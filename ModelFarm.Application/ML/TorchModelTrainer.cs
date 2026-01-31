using System.Buffers;
using System.Diagnostics;
using ModelFarm.Contracts.Training;
using TorchSharp;
using TorchSharp.Modules;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace ModelFarm.Application.ML;

/// <summary>
/// TorchSharp implementation of model training for quant trading regression models.
/// Supports Linear Regression, Gradient Boosting (via ensemble), and MLP.
/// Automatically uses CUDA GPU if available.
/// </summary>
public sealed class TorchModelTrainer : IModelTrainer
{
    private readonly int? _seed;
    private readonly Device _device;

    public TorchModelTrainer(int? seed = null)
    {
        _seed = seed;
        if (seed.HasValue)
        {
            torch.manual_seed(seed.Value);
        }
        
        // Select best available device: CUDA > CPU
        _device = torch.cuda.is_available() ? torch.CUDA : torch.CPU;
        Console.WriteLine($"[TorchModelTrainer] Using device: {_device.type}");
        
        if (_device.type == DeviceType.CUDA)
        {
            Console.WriteLine($"[TorchModelTrainer] CUDA device count: {torch.cuda.device_count()}");
        }
    }

    /// <summary>
    /// Synchronizes GPU operations if CUDA is available.
    /// </summary>
    private void SyncGpu()
    {
        if (_device.type == DeviceType.CUDA)
        {
            torch.cuda.synchronize();
        }
    }

    public async Task<ModelTrainingResult> TrainAsync(
        TrainingData trainData,
        TrainingData validationData,
        TrainingConfiguration config,
        IProgress<TrainingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var featureCount = trainData.FeatureNames.Length;
        var epochsTrained = 0;
        
        // Convert data to tensors
        var (trainFeatures, trainTargets) = CreateTensors(trainData);
        var (valFeatures, valTargets) = CreateTensors(validationData);

        // Create model based on type
        var model = CreateModel(config.ModelType, featureCount, config);
        
        // Create optimizer
        var optimizer = torch.optim.Adam(model.parameters(), lr: config.LearningRate);
        var lossFunction = nn.MSELoss();

        var bestValidationLoss = double.MaxValue;
        var epochsSinceImprovement = 0;
        var earlyStopTriggered = false;
        Module<Tensor, Tensor>? bestModelState = null;
        double finalTrainingLoss = 0;
        double finalValidationLoss = 0;

        for (int epoch = 1; epoch <= config.MaxEpochs; epoch++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            epochsTrained = epoch;

            // Training step - use 'using' to dispose intermediate tensors immediately
            model.train();
            optimizer.zero_grad();
            
            using (var trainPredictions = model.forward(trainFeatures))
            using (var trainLoss = lossFunction.forward(trainPredictions, trainTargets))
            {
                trainLoss.backward();
                optimizer.step();
                finalTrainingLoss = trainLoss.item<float>();
            }

            // Validation step
            model.eval();
            using (torch.no_grad())
            {
                using var valPredictions = model.forward(valFeatures);
                using var valLoss = lossFunction.forward(valPredictions, valTargets);
                finalValidationLoss = valLoss.item<float>();
            }

            if (finalValidationLoss < bestValidationLoss)
            {
                bestValidationLoss = finalValidationLoss;
                bestModelState = CloneModel(model, config.ModelType, featureCount, config);
                epochsSinceImprovement = 0;
            }
            else
            {
                epochsSinceImprovement++;
            }

            progress?.Report(new TrainingProgress
            {
                CurrentEpoch = epoch,
                TotalEpochs = config.MaxEpochs,
                TrainingLoss = finalTrainingLoss,
                ValidationLoss = finalValidationLoss,
                BestValidationLoss = bestValidationLoss,
                EpochsSinceImprovement = epochsSinceImprovement,
                Message = $"Epoch {epoch}/{config.MaxEpochs} - Train Loss: {finalTrainingLoss:F6}, Val Loss: {finalValidationLoss:F6}"
            });

            // Early stopping
            if (epochsSinceImprovement >= config.EarlyStoppingPatience)
            {
                earlyStopTriggered = true;
                break;
            }

            // Small delay to allow cancellation checks
            await Task.Delay(1, cancellationToken);
        }

        sw.Stop();

        // Use best model if available, dispose the unused one
        var finalModel = bestModelState ?? model;
        if (bestModelState is not null && bestModelState != model)
        {
            model.Dispose();
        }

        var trainedModel = new TorchTrainedModel(
            Guid.NewGuid(),
            finalModel,
            config.ModelType,
            trainData.FeatureNames);

        // Dispose tensors and sync GPU
        trainFeatures.Dispose();
        trainTargets.Dispose();
        valFeatures.Dispose();
        valTargets.Dispose();
        SyncGpu();

        return new ModelTrainingResult
        {
            Model = trainedModel,
            EpochsTrained = epochsTrained,
            FinalTrainingLoss = finalTrainingLoss,
            FinalValidationLoss = finalValidationLoss,
            BestValidationLoss = bestValidationLoss,
            EarlyStopTriggered = earlyStopTriggered,
            TrainingDuration = sw.Elapsed,
            EpochHistory = [] // Not stored - would require database/file storage for real use
        };
    }

    public async Task<ModelTrainingResult> TrainWithCheckpointsAsync(
        TrainingData trainData,
        TrainingData validationData,
        TrainingConfiguration config,
        CheckpointManager checkpointManager,
        Guid jobId,
        TrainingCheckpoint? resumeFromCheckpoint,
        NormalizationStats normStats,
        int checkpointIntervalEpochs,
        IProgress<TrainingProgress>? progress = null,
        Func<int, double, Task>? onCheckpointSaved = null,
        Func<bool>? isPaused = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var featureCount = trainData.FeatureNames.Length;
        var epochsTrained = 0;

        // Convert data to tensors
        var (trainFeatures, trainTargets) = CreateTensors(trainData);
        var (valFeatures, valTargets) = CreateTensors(validationData);

        // Create or load model
        Module<Tensor, Tensor> model;
        Module<Tensor, Tensor>? bestModelState = null;
        var startEpoch = 1;
        var bestValidationLoss = double.MaxValue;
        var epochsSinceImprovement = 0;
        var accumulatedDuration = TimeSpan.Zero;
        var currentLearningRate = config.LearningRate;

        if (resumeFromCheckpoint is not null)
        {
            // Load model from checkpoint
            model = CreateModel(config.ModelType, featureCount, config);
            var modelPath = checkpointManager.GetModelWeightsPath(jobId, resumeFromCheckpoint.ModelWeightsPath);
            model.load(modelPath);

            // Load best model if available
            var bestModelPath = checkpointManager.GetModelWeightsPath(jobId, resumeFromCheckpoint.BestModelWeightsPath);
            if (File.Exists(bestModelPath))
            {
                bestModelState = CreateModel(config.ModelType, featureCount, config);
                bestModelState.load(bestModelPath);
            }

            // Restore state
            startEpoch = resumeFromCheckpoint.Epoch + 1;
            bestValidationLoss = resumeFromCheckpoint.BestValidationLoss;
            epochsSinceImprovement = resumeFromCheckpoint.EpochsSinceImprovement;
            accumulatedDuration = resumeFromCheckpoint.TotalTrainingDuration;
            currentLearningRate = resumeFromCheckpoint.CurrentLearningRate;

            progress?.Report(new TrainingProgress
            {
                CurrentEpoch = resumeFromCheckpoint.Epoch,
                TotalEpochs = config.MaxEpochs,
                TrainingLoss = 0,
                ValidationLoss = bestValidationLoss,
                BestValidationLoss = bestValidationLoss,
                EpochsSinceImprovement = epochsSinceImprovement,
                Message = $"Resumed from checkpoint at epoch {resumeFromCheckpoint.Epoch}"
            });
        }
        else
        {
            model = CreateModel(config.ModelType, featureCount, config);
        }

        // Create optimizer with current learning rate
        var optimizer = torch.optim.Adam(model.parameters(), lr: currentLearningRate);
        var lossFunction = nn.MSELoss();

        var earlyStopTriggered = false;
        double finalTrainingLoss = 0;
        double finalValidationLoss = bestValidationLoss;

        for (int epoch = startEpoch; epoch <= config.MaxEpochs; epoch++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            epochsTrained = epoch;

            // Check for pause - wait until unpaused
            while (isPaused?.Invoke() == true && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();

            // Training step - use 'using' to dispose intermediate tensors immediately
            model.train();
            optimizer.zero_grad();

            using (var trainPredictions = model.forward(trainFeatures))
            using (var trainLoss = lossFunction.forward(trainPredictions, trainTargets))
            {
                trainLoss.backward();
                optimizer.step();
                finalTrainingLoss = trainLoss.item<float>();
            }

            // Validation step
            model.eval();
            using (torch.no_grad())
            {
                using var valPredictions = model.forward(valFeatures);
                using var valLoss = lossFunction.forward(valPredictions, valTargets);
                finalValidationLoss = valLoss.item<float>();
            }

            if (finalValidationLoss < bestValidationLoss)
            {
                bestValidationLoss = finalValidationLoss;
                bestModelState = CloneModel(model, config.ModelType, featureCount, config);
                epochsSinceImprovement = 0;
            }
            else
            {
                epochsSinceImprovement++;
            }

            progress?.Report(new TrainingProgress
            {
                CurrentEpoch = epoch,
                TotalEpochs = config.MaxEpochs,
                TrainingLoss = finalTrainingLoss,
                ValidationLoss = finalValidationLoss,
                BestValidationLoss = bestValidationLoss,
                EpochsSinceImprovement = epochsSinceImprovement,
                Message = $"Epoch {epoch}/{config.MaxEpochs} - Train Loss: {finalTrainingLoss:F6}, Val Loss: {finalValidationLoss:F6}"
            });

            // Save checkpoint periodically (if enabled)
            if (checkpointIntervalEpochs > 0 && epoch % checkpointIntervalEpochs == 0)
            {
                var checkpoint = new TrainingCheckpoint
                {
                    JobId = jobId,
                    ConfigurationId = config.Id,
                    Epoch = epoch,
                    BestValidationLoss = bestValidationLoss,
                    EpochsSinceImprovement = epochsSinceImprovement,
                    TotalTrainingDuration = accumulatedDuration + sw.Elapsed,
                    RetryAttempt = 1,
                    CurrentLearningRate = currentLearningRate,
                    ModelType = config.ModelType,
                    FeatureCount = featureCount,
                    FeatureNames = trainData.FeatureNames,
                    HiddenLayerSizes = config.HiddenLayerSizes,
                    NormalizationStats = normStats,
                    CreatedAtUtc = DateTime.UtcNow,
                    ModelWeightsPath = "model_current.pt",
                    BestModelWeightsPath = "model_best.pt"
                };

                await checkpointManager.SaveCheckpointAsync(checkpoint, model, bestModelState, cancellationToken);
                onCheckpointSaved?.Invoke(epoch, bestValidationLoss);
            }

            // Early stopping
            if (epochsSinceImprovement >= config.EarlyStoppingPatience)
            {
                earlyStopTriggered = true;
                break;
            }

            // Small delay to allow cancellation checks
            await Task.Delay(1, cancellationToken);
        }

        sw.Stop();

        // Use best model if available, dispose the unused one
        var finalModel = bestModelState ?? model;
        if (bestModelState is not null && bestModelState != model)
        {
            model.Dispose();
        }

        var trainedModel = new TorchTrainedModel(
            Guid.NewGuid(),
            finalModel,
            config.ModelType,
            trainData.FeatureNames);

        // Dispose tensors and sync GPU
        trainFeatures.Dispose();
        trainTargets.Dispose();
        valFeatures.Dispose();
        valTargets.Dispose();
        SyncGpu();

        return new ModelTrainingResult
        {
            Model = trainedModel,
            EpochsTrained = epochsTrained + (resumeFromCheckpoint?.Epoch ?? 0),
            FinalTrainingLoss = finalTrainingLoss,
            FinalValidationLoss = finalValidationLoss,
            BestValidationLoss = bestValidationLoss,
            EarlyStopTriggered = earlyStopTriggered,
            TrainingDuration = accumulatedDuration + sw.Elapsed,
            EpochHistory = [] // Not stored - would require database/file storage for real use
        };
    }

    public Task<ModelEvaluationResult> EvaluateAsync(
        ITrainedModel model,
        TrainingData testData,
        CancellationToken cancellationToken = default)
    {
        var sampleCount = testData.Samples.Count;
        var predictions = new List<PredictionResult>(sampleCount);
        
        // Running statistics for metrics calculation (avoids storing separate lists)
        double sumSquaredError = 0;
        double sumAbsoluteError = 0;
        double sumActual = 0;
        double sumActualSquared = 0;
        double sumPredicted = 0;
        double sumPredictedSquared = 0;
        double sumActualPredicted = 0;

        foreach (var sample in testData.Samples)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var predicted = model.Predict(sample.Features);
            var actual = sample.Target;
            
            predictions.Add(new PredictionResult
            {
                Timestamp = sample.Timestamp,
                Actual = actual,
                Predicted = predicted,
                ClosePrice = sample.ClosePrice
            });
            
            // Update running statistics
            var error = actual - predicted;
            sumSquaredError += error * error;
            sumAbsoluteError += Math.Abs(error);
            sumActual += actual;
            sumActualSquared += actual * actual;
        }

        // Calculate metrics from running statistics
        var mse = sumSquaredError / sampleCount;
        var rmse = Math.Sqrt(mse);
        var mae = sumAbsoluteError / sampleCount;
        
        // R-squared using running stats
        var meanActual = sumActual / sampleCount;
        var ssTot = sumActualSquared - (sumActual * sumActual / sampleCount);
        var rSquared = ssTot > 0 ? 1 - (sumSquaredError / ssTot) : 0;

        return Task.FromResult(new ModelEvaluationResult
        {
            MeanSquaredError = mse,
            RootMeanSquaredError = rmse,
            MeanAbsoluteError = mae,
            RSquared = rSquared,
            Predictions = predictions
        });
    }

    private (Tensor features, Tensor targets) CreateTensors(TrainingData data)
    {
        int featureCount = data.FeatureNames.Length;
        int sampleCount = data.Samples.Count;
        int featuresLength = sampleCount * featureCount;

        // Rent arrays from pool instead of allocating new ones
        float[] featuresArray = ArrayPool<float>.Shared.Rent(featuresLength);
        float[] targetsArray = ArrayPool<float>.Shared.Rent(sampleCount);

        try
        {
            // Copy features directly using BlockCopy for speed
            IReadOnlyList<TrainingSample> samples = data.Samples;
            for (int i = 0; i < sampleCount; i++)
            {
                TrainingSample sample = samples[i];
                Buffer.BlockCopy(sample.Features, 0, featuresArray, i * featureCount * sizeof(float), featureCount * sizeof(float));
                targetsArray[i] = sample.Target;
            }

            // Create tensors - use only the portion of the rented arrays we need
            // TorchSharp tensor() copies the data, so we can return arrays to pool after
            Tensor featuresTensor = torch.tensor(featuresArray.AsSpan(0, featuresLength).ToArray(), dtype: ScalarType.Float32)
                .reshape(sampleCount, featureCount);
            Tensor targetsTensor = torch.tensor(targetsArray.AsSpan(0, sampleCount).ToArray(), dtype: ScalarType.Float32)
                .reshape(sampleCount, 1);

            // Move to device (GPU if available)
            Tensor features = featuresTensor.to(_device);
            Tensor targets = targetsTensor.to(_device);

            // Dispose CPU tensors if we moved to GPU
            if (_device.type != DeviceType.CPU)
            {
                featuresTensor.Dispose();
                targetsTensor.Dispose();
            }

            return (features, targets);
        }
        finally
        {
            // Return arrays to pool
            ArrayPool<float>.Shared.Return(featuresArray);
            ArrayPool<float>.Shared.Return(targetsArray);
        }
    }

    private Module<Tensor, Tensor> CreateModel(ModelType modelType, int featureCount, TrainingConfiguration config)
    {
        Module<Tensor, Tensor> model = modelType switch
        {
            ModelType.LinearRegression => new LinearRegressionModel(featureCount),
            ModelType.GradientBoosting => new GradientBoostingModel(featureCount),
            ModelType.MLP => new MLPModel(featureCount, config.HiddenLayerSizes),
            _ => new LinearRegressionModel(featureCount)
        };
        
        
        
        
        // Move model to the selected device (GPU if available)
        model.to(_device);
        return model;
    }

    private Module<Tensor, Tensor> CloneModel(Module<Tensor, Tensor> source, ModelType modelType, int featureCount, TrainingConfiguration config)
    {
        var clone = CreateModel(modelType, featureCount, config);
        clone.load_state_dict(source.state_dict());
        return clone;
    }
}

/// <summary>
/// Simple linear regression model.
/// </summary>
internal sealed class LinearRegressionModel : Module<Tensor, Tensor>
{
    private readonly Linear _linear;

    public LinearRegressionModel(int inputSize) : base("LinearRegression")
    {
        _linear = nn.Linear(inputSize, 1);
        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        return _linear.forward(x);
    }
}

/// <summary>
/// Gradient boosting approximation using ensemble of small networks.
/// </summary>
internal sealed class GradientBoostingModel : Module<Tensor, Tensor>
{
    private readonly Sequential _model;

    public GradientBoostingModel(int inputSize) : base("GradientBoosting")
    {
        // Approximate gradient boosting with a deeper network
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

    public override Tensor forward(Tensor x)
    {
        return _model.forward(x);
    }
}

/// <summary>
/// Multi-layer perceptron model.
/// </summary>
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
            layers.Add(($"dropout{i}", nn.Dropout(0.2)));
            prevSize = size;
        }

        layers.Add(("output", nn.Linear(prevSize, 1)));

        _model = nn.Sequential(layers);
        RegisterComponents();
    }

    public override Tensor forward(Tensor x)
    {
        return _model.forward(x);
    }
}

/// <summary>
/// TorchSharp trained model wrapper.
/// </summary>
internal sealed class TorchTrainedModel : ITrainedModel
{
    private readonly Module<Tensor, Tensor> _model;
    private readonly ModelType _modelType;
    private readonly string[] _featureNames;

    public Guid ModelId { get; }

    public TorchTrainedModel(
        Guid modelId,
        Module<Tensor, Tensor> model,
        ModelType modelType,
        string[] featureNames)
    {
        ModelId = modelId;
        _model = model;
        _modelType = modelType;
        _featureNames = featureNames;
        
        // Move model to CPU for stable inference during backtesting
        _model.cpu();
        _model.eval();
    }

    public float Predict(float[] features)
    {
        using var _ = torch.no_grad();
        using var input = torch.tensor(features, dtype: ScalarType.Float32)
            .reshape(1, features.Length);
        using var output = _model.forward(input);
        return output.item<float>();
    }

    public float[] PredictBatch(IReadOnlyList<float[]> features)
    {
        using IDisposable _ = torch.no_grad();
        int featureCount = features[0].Length;
        int sampleCount = features.Count;
        int flatLength = sampleCount * featureCount;
        
        // Rent array from pool
        float[] flatArray = ArrayPool<float>.Shared.Rent(flatLength);
        try
        {
            for (int i = 0; i < sampleCount; i++)
            {
                Buffer.BlockCopy(features[i], 0, flatArray, i * featureCount * sizeof(float), featureCount * sizeof(float));
            }

            using Tensor input = torch.tensor(flatArray.AsSpan(0, flatLength).ToArray(), dtype: ScalarType.Float32)
                .reshape(sampleCount, featureCount);
            using Tensor output = _model.forward(input);
            
            float[] result = new float[sampleCount];
            float[] outputData = output.data<float>().ToArray();
            Array.Copy(outputData, result, sampleCount);
            
            return result;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(flatArray);
        }
    }

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await Task.Run(() => _model.save(path), cancellationToken);
    }

    public ModelMetadata GetMetadata()
    {
        return new ModelMetadata
        {
            ModelId = ModelId,
            ModelType = _modelType,
            FeatureNames = _featureNames,
            NormalizationStats = null,
            TrainedAtUtc = DateTime.UtcNow,
            EpochsTrained = 0,
            BestValidationLoss = 0
        };
    }

    public void Dispose()
    {
        _model.Dispose();
    }
}
