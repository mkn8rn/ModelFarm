using Microsoft.EntityFrameworkCore;
using ModelFarm.Application.ML;
using ModelFarm.Contracts.Testing;
using ModelFarm.Contracts.Training;
using ModelFarm.Infrastructure.Persistence;
using ModelFarm.Infrastructure.Persistence.Entities;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for testing trained models on datasets.
/// </summary>
public sealed class ModelTestingService : IModelTestingService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly ITrainingService _trainingService;
    private readonly IDatasetService _datasetService;
    private readonly BacktestEngine _backtestEngine;
    private readonly CheckpointManager _checkpointManager;

    public ModelTestingService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        ITrainingService trainingService,
        IDatasetService datasetService,
        BacktestEngine backtestEngine)
    {
        _dbContextFactory = dbContextFactory;
        _trainingService = trainingService;
        _datasetService = datasetService;
        _backtestEngine = backtestEngine;
        _checkpointManager = new CheckpointManager();
    }

    public async Task<IReadOnlyList<AvailableModel>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        var completedJobs = await _trainingService.GetAllTrainingJobsAsync(TrainingJobStatus.Completed, cancellationToken);
        var result = new List<AvailableModel>();

        foreach (var job in completedJobs)
        {
            if (!_checkpointManager.CheckpointExists(job.Id))
                continue;

            var config = await _trainingService.GetConfigurationAsync(job.ConfigurationId, cancellationToken);
            if (config is null)
                continue;

            var dataset = await _datasetService.GetDatasetAsync(config.DatasetId, cancellationToken);

            result.Add(new AvailableModel
            {
                JobId = job.Id,
                JobName = job.Name,
                ConfigurationName = config.Name,
                ModelType = config.ModelType,
                DatasetId = config.DatasetId,
                DatasetName = dataset?.Name ?? "Unknown",
                Symbol = dataset?.Symbol ?? "Unknown",
                CompletedAtUtc = job.CompletedAtUtc ?? DateTime.UtcNow,
                SharpeRatio = job.Result?.BacktestMetrics.SharpeRatio,
                MeetsRequirements = job.Result?.MeetsRequirements ?? false
            });
        }

        return result;
    }

    public async Task<LoadedModel?> LoadModelAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _trainingService.GetTrainingJobAsync(jobId, cancellationToken);
        if (job is null || job.Status != TrainingJobStatus.Completed)
            return null;

        var checkpoint = await _checkpointManager.LoadCheckpointAsync(jobId, cancellationToken);
        if (checkpoint is null)
            return null;

        var config = await _trainingService.GetConfigurationAsync(job.ConfigurationId, cancellationToken);
        if (config is null)
            return null;

        // Load the best model weights
        var bestModelPath = _checkpointManager.GetModelWeightsPath(jobId, checkpoint.BestModelWeightsPath);
        if (!File.Exists(bestModelPath))
            return null;

        var torchModel = CreateModel(checkpoint.ModelType, checkpoint.FeatureCount, checkpoint.HiddenLayerSizes);
        torchModel.load(bestModelPath);
        torchModel.eval();

        var trainedModel = new TorchTrainedModel(
            torchModel,
            jobId,
            checkpoint.ModelType,
            checkpoint.FeatureNames);

        return new LoadedModel
        {
            JobId = jobId,
            JobName = job.Name,
            ConfigurationId = job.ConfigurationId,
            ModelType = checkpoint.ModelType,
            MaxLags = config.MaxLags,
            FeatureNames = checkpoint.FeatureNames,
            NormalizationStats = checkpoint.NormalizationStats,
            TradingConfig = config.TradingEnvironment,
            Model = trainedModel
        };
    }

    public async Task<ModelTest> CreateAndRunTestAsync(
        Guid modelJobId,
        Guid datasetId,
        string? testName = null,
        CancellationToken cancellationToken = default)
    {
        // Get model and dataset info for the test record
        var availableModels = await GetAvailableModelsAsync(cancellationToken);
        var modelInfo = availableModels.FirstOrDefault(m => m.JobId == modelJobId)
            ?? throw new InvalidOperationException("Model not found");

        var dataset = await _datasetService.GetDatasetAsync(datasetId, cancellationToken)
            ?? throw new InvalidOperationException("Dataset not found");

        // Generate test name if not provided
        var name = string.IsNullOrWhiteSpace(testName)
            ? $"{modelInfo.JobName} on {dataset.Name}"
            : testName;

        // Create test record
        var test = new ModelTest
        {
            Id = Guid.NewGuid(),
            Name = name,
            ModelJobId = modelJobId,
            DatasetId = datasetId,
            ModelName = modelInfo.JobName,
            ModelType = modelInfo.ModelType.ToString(),
            DatasetName = dataset.Name,
            Symbol = dataset.Symbol,
            Status = ModelTestStatus.Running,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Save initial test record
        await using (var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            db.ModelTests.Add(ModelTestEntity.FromModelTest(test));
            await db.SaveChangesAsync(cancellationToken);
        }

        try
        {
            // Load model and run test
            using var model = await LoadModelAsync(modelJobId, cancellationToken)
                ?? throw new InvalidOperationException("Failed to load model");

            var result = await RunTestInternalAsync(model, datasetId, cancellationToken);

            // Update test with results
            test = test with
            {
                Status = ModelTestStatus.Completed,
                CompletedAtUtc = DateTime.UtcNow,
                Result = result
            };
        }
        catch (Exception ex)
        {
            test = test with
            {
                Status = ModelTestStatus.Failed,
                CompletedAtUtc = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }

        // Save final test record
        await using (var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var entity = await db.ModelTests.FindAsync([test.Id], cancellationToken);
            if (entity is not null)
            {
                var updated = ModelTestEntity.FromModelTest(test);
                entity.Status = updated.Status;
                entity.CompletedAtUtc = updated.CompletedAtUtc;
                entity.ErrorMessage = updated.ErrorMessage;
                entity.ResultJson = updated.ResultJson;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return test;
    }

    public async Task<IReadOnlyList<ModelTest>> GetAllTestsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.ModelTests
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToModelTest()).ToList();
    }

    public async Task<ModelTest?> GetTestAsync(Guid testId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ModelTests.FindAsync([testId], cancellationToken);
        return entity?.ToModelTest();
    }

    public async Task<bool> DeleteTestAsync(Guid testId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ModelTests.FindAsync([testId], cancellationToken);
        if (entity is null)
            return false;

        db.ModelTests.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<ModelTestResult> RunTestInternalAsync(
        LoadedModel model,
        Guid datasetId,
        CancellationToken cancellationToken)
    {
        // Get dataset klines
        var klines = await _datasetService.GetDatasetKlinesAsync(datasetId, cancellationToken);
        if (klines.Count == 0)
            throw new InvalidOperationException("Dataset has no data");

        // Prepare features using same logic as training
        var testData = FeatureEngineering.PrepareTrainingData(klines, model.MaxLags);

        // Normalize using model's normalization stats
        var normalizedData = FeatureEngineering.ApplyNormalization(testData, model.NormalizationStats);

        // Run predictions
        var predictions = new List<TestPrediction>();
        var predictionResults = new List<PredictionResult>();
        double sumSquaredError = 0;
        double sumAbsoluteError = 0;
        int correctDirection = 0;

        foreach (var sample in normalizedData.Samples)
        {
            var predicted = model.Model.Predict(sample.Features);
            var actual = sample.Target;

            var error = predicted - actual;
            sumSquaredError += error * error;
            sumAbsoluteError += Math.Abs(error);

            // Directional accuracy: did we predict the correct sign?
            if ((predicted > 0 && actual > 0) || (predicted < 0 && actual < 0) || (predicted == 0 && actual == 0))
                correctDirection++;

            var signal = predicted > 0 ? "BUY" : predicted < 0 ? "SELL" : "HOLD";

            predictions.Add(new TestPrediction
            {
                Timestamp = sample.Timestamp,
                ClosePrice = (decimal)sample.ClosePrice,
                ActualReturn = actual,
                PredictedReturn = predicted,
                Signal = signal
            });

            predictionResults.Add(new PredictionResult
            {
                Timestamp = sample.Timestamp,
                Actual = actual,
                Predicted = predicted,
                ClosePrice = sample.ClosePrice
            });
        }

        var totalPredictions = predictions.Count;
        var mse = totalPredictions > 0 ? sumSquaredError / totalPredictions : 0;
        var mae = totalPredictions > 0 ? sumAbsoluteError / totalPredictions : 0;
        var directionalAccuracy = totalPredictions > 0 ? (double)correctDirection / totalPredictions : 0;

        // Run backtest
        var config = await _trainingService.GetConfigurationAsync(model.ConfigurationId, cancellationToken);
        var dataset = await _datasetService.GetDatasetAsync(datasetId, cancellationToken);
        var annualizationFactor = CalculateAnnualizationFactor(dataset?.Interval ?? Contracts.MarketData.KlineInterval.OneHour);

        var backtestResult = _backtestEngine.RunBacktest(predictionResults, model.TradingConfig, annualizationFactor);

        // Limit predictions returned to avoid large payloads
        var limitedPredictions = predictions.Count > 500
            ? predictions.Take(250).Concat(predictions.Skip(predictions.Count - 250)).ToList()
            : predictions;

        return new ModelTestResult
        {
            TotalPredictions = totalPredictions,
            MeanSquaredError = mse,
            MeanAbsoluteError = mae,
            DirectionalAccuracy = directionalAccuracy,
            BacktestResult = backtestResult,
            Predictions = limitedPredictions
        };
    }

    private static Module<Tensor, Tensor> CreateModel(ModelType modelType, int featureCount, int[] hiddenLayerSizes)
    {
        return modelType switch
        {
            ModelType.LinearRegression => new LinearRegressionModel(featureCount),
            ModelType.GradientBoosting => new GradientBoostingModel(featureCount),
            ModelType.MLP => new MLPModel(featureCount, hiddenLayerSizes),
            _ => throw new ArgumentException($"Unknown model type: {modelType}")
        };
    }

    private static double CalculateAnnualizationFactor(Contracts.MarketData.KlineInterval interval)
    {
        var periodsPerYear = interval switch
        {
            Contracts.MarketData.KlineInterval.OneMinute => 365.25 * 24 * 60,
            Contracts.MarketData.KlineInterval.FiveMinutes => 365.25 * 24 * 12,
            Contracts.MarketData.KlineInterval.FifteenMinutes => 365.25 * 24 * 4,
            Contracts.MarketData.KlineInterval.OneHour => 365.25 * 24,
            Contracts.MarketData.KlineInterval.FourHours => 365.25 * 6,
            Contracts.MarketData.KlineInterval.OneDay => 365.25,
            _ => 365.25 * 24
        };
        return Math.Sqrt(periodsPerYear);
    }
}

// Internal model classes matching TorchModelTrainer
internal sealed class LinearRegressionModel : Module<Tensor, Tensor>
{
    private readonly Linear _linear;

    public LinearRegressionModel(int inputSize) : base("LinearRegression")
    {
        _linear = nn.Linear(inputSize, 1);
        RegisterComponents();
    }

    public override Tensor forward(Tensor x) => _linear.forward(x);
}

internal sealed class GradientBoostingModel : Module<Tensor, Tensor>
{
    private readonly Sequential _model;

    public GradientBoostingModel(int inputSize) : base("GradientBoosting")
    {
        _model = nn.Sequential(
            ("layer1", nn.Linear(inputSize, 64)),
            ("relu1", nn.ReLU()),
            ("dropout1", nn.Dropout(0.1)),
            ("layer2", nn.Linear(64, 32)),
            ("relu2", nn.ReLU()),
            ("dropout2", nn.Dropout(0.1)),
            ("layer3", nn.Linear(32, 16)),
            ("relu3", nn.ReLU()),
            ("output", nn.Linear(16, 1))
        );
        RegisterComponents();
    }

    public override Tensor forward(Tensor x) => _model.forward(x);
}

internal sealed class MLPModel : Module<Tensor, Tensor>
{
    private readonly Sequential _model;

    public MLPModel(int inputSize, int[]? hiddenSizes = null) : base("MLP")
    {
        hiddenSizes ??= [64, 32];

        var layers = new List<(string name, Module<Tensor, Tensor> module)>();
        var prevSize = inputSize;

        for (int i = 0; i < hiddenSizes.Length; i++)
        {
            var size = hiddenSizes[i];
            layers.Add(($"linear{i}", nn.Linear(prevSize, size)));
            layers.Add(($"relu{i}", nn.ReLU()));
            prevSize = size;
        }

        layers.Add(("output", nn.Linear(prevSize, 1)));
        _model = nn.Sequential(layers);
        RegisterComponents();
    }

    public override Tensor forward(Tensor x) => _model.forward(x);
}

/// <summary>
/// Wrapper for a TorchSharp model that implements ITrainedModel.
/// </summary>
internal sealed class TorchTrainedModel : ITrainedModel
{
    private readonly Module<Tensor, Tensor> _model;
    private readonly ModelType _modelType;
    private readonly string[] _featureNames;

    public Guid ModelId { get; }

    public TorchTrainedModel(
        Module<Tensor, Tensor> model,
        Guid modelId,
        ModelType modelType,
        string[] featureNames)
    {
        _model = model;
        ModelId = modelId;
        _modelType = modelType;
        _featureNames = featureNames;
    }

    public float Predict(float[] features)
    {
        using var _ = torch.no_grad();
        using var input = torch.tensor(features, dtype: ScalarType.Float32).reshape(1, features.Length);
        using var output = _model.forward(input);
        return output.item<float>();
    }

    public float[] PredictBatch(IReadOnlyList<float[]> features)
    {
        using var _ = torch.no_grad();
        var featureCount = features[0].Length;
        var flatArray = new float[features.Count * featureCount];

        for (int i = 0; i < features.Count; i++)
            Array.Copy(features[i], 0, flatArray, i * featureCount, featureCount);

        using var input = torch.tensor(flatArray, dtype: ScalarType.Float32).reshape(features.Count, featureCount);
        using var output = _model.forward(input);

        return output.data<float>().ToArray();
    }

    public Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => _model.save(path), cancellationToken);
    }

    public ModelMetadata GetMetadata() => new()
    {
        ModelId = ModelId,
        ModelType = _modelType,
        FeatureNames = _featureNames,
        NormalizationStats = null,
        TrainedAtUtc = DateTime.UtcNow,
        EpochsTrained = 0,
        BestValidationLoss = 0
    };

    public void Dispose() => _model.Dispose();
}
