using System.Collections.Concurrent;
using ModelFarm.Application.ML;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.Services;

public sealed class TrainingService : ITrainingService
{
    private readonly IDatasetService _datasetService;
    private readonly IModelTrainer _modelTrainer;
    private readonly BacktestEngine _backtestEngine;
    
    private static readonly ConcurrentDictionary<Guid, TrainingConfiguration> _configurations = new();
    private static readonly ConcurrentDictionary<Guid, TrainingJobState> _jobs = new();

    public TrainingService(
        IDatasetService datasetService,
        IModelTrainer modelTrainer,
        BacktestEngine backtestEngine)
    {
        _datasetService = datasetService;
        _modelTrainer = modelTrainer;
        _backtestEngine = backtestEngine;
    }

    // ==================== Configuration Management ====================

    public Task<TrainingConfiguration> CreateConfigurationAsync(CreateTrainingConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        var config = new TrainingConfiguration
        {
            Id = Guid.NewGuid(),
            Type = request.Type,
            Name = request.Name,
            Description = request.Description,
            DatasetId = request.DatasetId,
            ModelType = request.ModelType,
            MaxLags = request.MaxLags,
            ForecastHorizon = request.ForecastHorizon,
            HiddenLayerSizes = request.HiddenLayerSizes,
            DropoutRate = request.DropoutRate,
            LearningRate = request.LearningRate,
            BatchSize = request.BatchSize,
            MaxEpochs = request.MaxEpochs,
            EarlyStoppingPatience = request.EarlyStoppingPatience,
            ValidationSplit = request.ValidationSplit,
            TestSplit = request.TestSplit,
            RandomSeed = request.RandomSeed,
            PerformanceRequirements = request.PerformanceRequirements,
            TradingEnvironment = request.TradingEnvironment,
            CreatedAtUtc = DateTime.UtcNow
        };

        _configurations[config.Id] = config;
        return Task.FromResult(config);
    }

    public Task<TrainingConfiguration?> GetConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        _configurations.TryGetValue(configurationId, out var config);
        return Task.FromResult(config);
    }

    public Task<IReadOnlyList<TrainingConfiguration>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<TrainingConfiguration>>(_configurations.Values.OrderByDescending(c => c.CreatedAtUtc).ToList());
    }

    public Task<TrainingConfiguration> UpdateConfigurationAsync(Guid configurationId, UpdateTrainingConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        if (!_configurations.TryGetValue(configurationId, out var existing))
            throw new KeyNotFoundException($"Configuration {configurationId} not found");

        var updated = existing with
        {
            Name = request.Name ?? existing.Name,
            Description = request.Description ?? existing.Description,
            MaxLags = request.MaxLags ?? existing.MaxLags,
            ForecastHorizon = request.ForecastHorizon ?? existing.ForecastHorizon,
            HiddenLayerSizes = request.HiddenLayerSizes ?? existing.HiddenLayerSizes,
            DropoutRate = request.DropoutRate ?? existing.DropoutRate,
            LearningRate = request.LearningRate ?? existing.LearningRate,
            BatchSize = request.BatchSize ?? existing.BatchSize,
            MaxEpochs = request.MaxEpochs ?? existing.MaxEpochs,
            EarlyStoppingPatience = request.EarlyStoppingPatience ?? existing.EarlyStoppingPatience,
            ValidationSplit = request.ValidationSplit ?? existing.ValidationSplit,
            TestSplit = request.TestSplit ?? existing.TestSplit,
            RandomSeed = request.RandomSeed ?? existing.RandomSeed,
            PerformanceRequirements = request.PerformanceRequirements ?? existing.PerformanceRequirements,
            TradingEnvironment = request.TradingEnvironment ?? existing.TradingEnvironment,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _configurations[configurationId] = updated;
        return Task.FromResult(updated);
    }

    public Task<bool> DeleteConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_configurations.TryRemove(configurationId, out _));
    }

    // ==================== Training Job Management ====================

    public async Task<TrainingJob> StartTrainingAsync(TrainingJobRequest request, CancellationToken cancellationToken = default)
    {
        if (!_configurations.TryGetValue(request.ConfigurationId, out var config))
            throw new KeyNotFoundException($"Configuration {request.ConfigurationId} not found");

        var dataset = await _datasetService.GetDatasetAsync(config.DatasetId, cancellationToken);
        if (dataset is null)
            throw new KeyNotFoundException($"Dataset {config.DatasetId} not found");

        var jobId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var initialStatus = dataset.Status switch
        {
            DatasetStatus.Ready => TrainingJobStatus.Queued,
            DatasetStatus.Downloading => TrainingJobStatus.WaitingForData,
            DatasetStatus.Pending => TrainingJobStatus.WaitingForData,
            _ => TrainingJobStatus.Failed
        };

        var maxAttempts = request.ExecutionOptions.RetryUntilSuccess 
            ? request.ExecutionOptions.MaxRetryAttempts 
            : 1;

        var job = new TrainingJob
        {
            Id = jobId,
            Name = request.JobName ?? $"{config.Name} - {now:yyyy-MM-dd HH:mm}",
            ConfigurationId = request.ConfigurationId,
            Status = initialStatus,
            CurrentEpoch = 0,
            TotalEpochs = config.MaxEpochs,
            CurrentAttempt = 1,
            MaxAttempts = maxAttempts,
            Message = initialStatus == TrainingJobStatus.WaitingForData 
                ? "Waiting for dataset download to complete..." 
                : "Queued for training",
            CreatedAtUtc = now
        };

        var state = new TrainingJobState
        {
            Job = job,
            Configuration = config,
            Dataset = dataset,
            Overrides = request.Overrides,
            ExecutionOptions = request.ExecutionOptions,
            CancellationTokenSource = new CancellationTokenSource()
        };

        _jobs[jobId] = state;

        // Start training in background if data is ready
        if (initialStatus == TrainingJobStatus.Queued)
        {
            _ = RunTrainingAsync(state);
        }

        return job;
    }

    public Task<TrainingJob?> GetTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_jobs.TryGetValue(jobId, out var state))
            return Task.FromResult<TrainingJob?>(state.Job);
        return Task.FromResult<TrainingJob?>(null);
    }

    public Task<IReadOnlyList<TrainingJob>> GetAllTrainingJobsAsync(TrainingJobStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        var jobs = _jobs.Values.Select(s => s.Job);
        if (statusFilter.HasValue)
            jobs = jobs.Where(j => j.Status == statusFilter.Value);
        return Task.FromResult<IReadOnlyList<TrainingJob>>(jobs.OrderByDescending(j => j.CreatedAtUtc).ToList());
    }

    public Task<bool> CancelTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return Task.FromResult(false);

        state.CancellationTokenSource.Cancel();
        state.Job = state.Job with
        {
            Status = TrainingJobStatus.Cancelled,
            Message = "Training cancelled by user",
            CompletedAtUtc = DateTime.UtcNow
        };

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<TrainingJob>> GetJobsForConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        var jobs = _jobs.Values
            .Where(s => s.Job.ConfigurationId == configurationId)
            .Select(s => s.Job)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<TrainingJob>>(jobs);
    }

    private async Task RunTrainingAsync(TrainingJobState state)
    {
        var config = state.Configuration;
        var execOptions = state.ExecutionOptions;
        var cts = state.CancellationTokenSource;

        try
        {
            // Update status to preprocessing
            state.Job = state.Job with
            {
                Status = TrainingJobStatus.Preprocessing,
                StartedAtUtc = DateTime.UtcNow,
                Message = "Loading and preparing data..."
            };

            // Load kline data
            var klines = await _datasetService.GetDatasetKlinesAsync(config.DatasetId, cts.Token);
            if (klines.Count == 0)
                throw new InvalidOperationException($"Dataset contains no data. The dataset '{state.Dataset.Name}' ({state.Dataset.Symbol}, {state.Dataset.Interval}) has 0 records in the specified date range.");

            var minRequired = config.MaxLags + config.ForecastHorizon + 10;
            if (klines.Count < minRequired)
                throw new InvalidOperationException($"Dataset has insufficient data ({klines.Count} records). Need at least {minRequired} records for MaxLags={config.MaxLags} and ForecastHorizon={config.ForecastHorizon}.");

            state.Job = state.Job with { Message = $"Preparing features from {klines.Count} records..." };

            // Prepare features using the same approach as the notebook
            var allData = FeatureEngineering.PrepareTrainingData(klines, config.MaxLags, config.ForecastHorizon);
            
            state.Job = state.Job with { Message = $"Splitting {allData.Samples.Count} samples into train/val/test..." };

            // Split into train/validation/test
            var (trainData, validationData, testData) = FeatureEngineering.SplitData(
                allData, 
                config.ValidationSplit, 
                config.TestSplit);

            // Normalize features
            var (normalizedTrain, normStats) = FeatureEngineering.NormalizeFeatures(trainData);
            var normalizedValidation = FeatureEngineering.ApplyNormalization(validationData, normStats);
            var normalizedTest = FeatureEngineering.ApplyNormalization(testData, normStats);

            // Apply hyperparameter overrides to create effective config
            var effectiveConfig = ApplyOverrides(config, state.Overrides);
            
            // Retry loop
            TrainingResult? finalResult = null;
            var totalTrainingDuration = TimeSpan.Zero;
            
            do
            {
                state.RetryAttempt++;
                cts.Token.ThrowIfCancellationRequested();

                // Check for pause
                while (state.IsPaused && !cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(500, cts.Token);
                }
                
                // Adjust learning rate on retry if enabled
                if (state.RetryAttempt > 1 && execOptions.AdjustLearningRateOnRetry)
                {
                    effectiveConfig = effectiveConfig with
                    {
                        LearningRate = effectiveConfig.LearningRate * execOptions.LearningRateRetryFactor
                    };
                }

                // Shuffle training data on retry if enabled
                var trainDataForRun = normalizedTrain;
                if (state.RetryAttempt > 1 && execOptions.ShuffleOnRetry)
                {
                    trainDataForRun = ShuffleTrainingData(normalizedTrain, state.RetryAttempt);
                }

                var maxAttempts = execOptions.RetryUntilSuccess ? execOptions.MaxRetryAttempts : 1;
                state.Job = state.Job with
                {
                    Status = TrainingJobStatus.Training,
                    CurrentEpoch = 0,
                    CurrentAttempt = state.RetryAttempt,
                    MaxAttempts = maxAttempts,
                    Message = $"Training on {trainData.Samples.Count} samples..."
                };

                // Create progress reporter
                var progress = new Progress<TrainingProgress>(p =>
                {
                    state.Job = state.Job with
                    {
                        CurrentEpoch = p.CurrentEpoch,
                        TrainingLoss = p.TrainingLoss,
                        ValidationLoss = p.ValidationLoss,
                        BestValidationLoss = p.BestValidationLoss,
                        EpochsSinceImprovement = p.EpochsSinceImprovement,
                        Message = p.Message
                    };
                });

                // Apply early stopping setting
                var runConfig = execOptions.UseEarlyStopping 
                    ? effectiveConfig 
                    : effectiveConfig with { EarlyStoppingPatience = int.MaxValue };

                // Train the model
                var trainingResult = await _modelTrainer.TrainAsync(
                    trainDataForRun,
                    normalizedValidation,
                    runConfig,
                    progress,
                    cts.Token);

                totalTrainingDuration += trainingResult.TrainingDuration;

                state.Job = state.Job with
                {
                    Status = TrainingJobStatus.Backtesting,
                    Message = "Running backtest on test data..."
                };

                // Evaluate on test set
                var evalResult = await _modelTrainer.EvaluateAsync(trainingResult.Model, normalizedTest, cts.Token);

                // Run backtest
                var annualizationFactor = CalculateAnnualizationFactor(state.Dataset.Interval);
                var backtestResult = _backtestEngine.RunBacktest(
                    evalResult.Predictions, 
                    config.TradingEnvironment,
                    annualizationFactor);

                // Build result for this attempt
                var meetsRequirements = CheckRequirements(backtestResult.Metrics, config.PerformanceRequirements);

                finalResult = new TrainingResult
                {
                    FinalTrainingLoss = trainingResult.FinalTrainingLoss,
                    FinalValidationLoss = trainingResult.FinalValidationLoss,
                    TestLoss = evalResult.MeanSquaredError,
                    EpochsTrained = trainingResult.EpochsTrained,
                    EarlyStoppedTriggered = trainingResult.EarlyStopTriggered,
                    TrainingDuration = totalTrainingDuration,
                    MeetsRequirements = meetsRequirements,
                    BacktestMetrics = backtestResult.Metrics,
                    RetryAttempts = state.RetryAttempt
                };

                // If meets requirements or retry not enabled, we're done
                if (meetsRequirements || !execOptions.RetryUntilSuccess)
                {
                    break;
                }

                // Check if we've exhausted retries
                if (state.RetryAttempt >= execOptions.MaxRetryAttempts)
                {
                    state.Job = state.Job with
                    {
                        Message = $"Max retries ({execOptions.MaxRetryAttempts}) reached without meeting requirements"
                    };
                    break;
                }

                // Brief pause before retry
                state.Job = state.Job with
                {
                    Message = $"Attempt {state.RetryAttempt} did not meet requirements. Retrying..."
                };
                await Task.Delay(100, cts.Token);

            } while (execOptions.RetryUntilSuccess && state.RetryAttempt < execOptions.MaxRetryAttempts);

            var completionMsg = finalResult!.MeetsRequirements 
                ? $"Training completed - meets requirements" 
                : $"Training completed - does not meet requirements";
            
            if (state.RetryAttempt > 1)
            {
                completionMsg += $" (after {state.RetryAttempt} attempts)";
            }

            state.Job = state.Job with
            {
                Status = TrainingJobStatus.Completed,
                Message = completionMsg,
                CompletedAtUtc = DateTime.UtcNow,
                Result = finalResult
            };
        }
        catch (OperationCanceledException)
        {
            state.Job = state.Job with
            {
                Status = TrainingJobStatus.Cancelled,
                Message = "Training cancelled",
                CompletedAtUtc = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            state.Job = state.Job with
            {
                Status = TrainingJobStatus.Failed,
                Message = $"Training failed: {ex.Message}",
                ErrorMessage = ex.Message,
                CompletedAtUtc = DateTime.UtcNow
            };
        }
    }

    private static TrainingConfiguration ApplyOverrides(TrainingConfiguration config, HyperparameterOverrides? overrides)
    {
        if (overrides is null)
            return config;

        return config with
        {
            LearningRate = overrides.LearningRate ?? config.LearningRate,
            BatchSize = overrides.BatchSize ?? config.BatchSize,
            MaxEpochs = overrides.MaxEpochs ?? config.MaxEpochs,
            MaxLags = overrides.MaxLags ?? config.MaxLags,
            DropoutRate = overrides.DropoutRate ?? config.DropoutRate
        };
    }

    private static TrainingData ShuffleTrainingData(TrainingData data, int seed)
    {
        var rng = new Random(seed);
        var shuffledSamples = data.Samples.OrderBy(_ => rng.Next()).ToList();
        return data with { Samples = shuffledSamples };
    }

    private static double CalculateAnnualizationFactor(KlineInterval interval)
    {
        // Calculate annualization factor based on interval
        // For Sharpe ratio: sqrt(periods per year)
        var periodsPerYear = interval switch
        {
            KlineInterval.OneMinute => 365.25 * 24 * 60,
            KlineInterval.FiveMinutes => 365.25 * 24 * 12,
            KlineInterval.FifteenMinutes => 365.25 * 24 * 4,
            KlineInterval.OneHour => 365.25 * 24,
            KlineInterval.FourHours => 365.25 * 6,
            KlineInterval.OneDay => 365.25,
            _ => 365.25 * 24 // Default to hourly
        };

        return Math.Sqrt(periodsPerYear);
    }

    private static bool CheckRequirements(BacktestMetrics metrics, PerformanceRequirements req)
    {
        if (req.MinSharpeRatio.HasValue && metrics.SharpeRatio < req.MinSharpeRatio.Value) return false;
        if (req.MinSortinoRatio.HasValue && metrics.SortinoRatio < req.MinSortinoRatio.Value) return false;
        if (req.MaxDrawdown.HasValue && metrics.MaxDrawdown > req.MaxDrawdown.Value) return false;
        if (req.MinWinRate.HasValue && metrics.WinRate < req.MinWinRate.Value) return false;
        if (req.MinProfitFactor.HasValue && metrics.ProfitFactor < req.MinProfitFactor.Value) return false;
        if (req.MinAnnualizedReturn.HasValue && metrics.AnnualizedReturn < req.MinAnnualizedReturn.Value) return false;
        if (req.MaxConsecutiveLosses.HasValue && metrics.MaxConsecutiveLosses > req.MaxConsecutiveLosses.Value) return false;
        if (metrics.TotalTrades < req.MinTradeCount) return false;
        return true;
    }

    private sealed class TrainingJobState
    {
        public required TrainingJob Job { get; set; }
        public required TrainingConfiguration Configuration { get; init; }
        public required DatasetDefinition Dataset { get; init; }
        public HyperparameterOverrides? Overrides { get; init; }
        public TrainingExecutionOptions ExecutionOptions { get; init; } = new();
        public required CancellationTokenSource CancellationTokenSource { get; init; }
        public int RetryAttempt { get; set; } = 0;
        public bool IsPaused { get; set; } = false;
    }

    public Task<bool> PauseTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return Task.FromResult(false);

        if (state.Job.Status is not (TrainingJobStatus.Training or TrainingJobStatus.Backtesting or TrainingJobStatus.Preprocessing))
            return Task.FromResult(false);

        state.IsPaused = true;
        state.Job = state.Job with
        {
            IsPaused = true,
            Message = "Paused"
        };

        return Task.FromResult(true);
    }

    public Task<bool> ResumeTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return Task.FromResult(false);

        if (!state.IsPaused)
            return Task.FromResult(false);

        state.IsPaused = false;
        state.Job = state.Job with
        {
            IsPaused = false,
            Message = "Resumed"
        };

        return Task.FromResult(true);
    }

    public Task<bool> RetryTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_jobs.TryGetValue(jobId, out var state))
            return Task.FromResult(false);

        // Can only retry failed or completed jobs that didn't meet requirements
        if (state.Job.Status is not (TrainingJobStatus.Failed or TrainingJobStatus.Completed))
            return Task.FromResult(false);

        // Reset state for retry
        state.RetryAttempt = 0;
        state.IsPaused = false;
        state.CancellationTokenSource.TryReset();

        state.Job = state.Job with
        {
            Status = TrainingJobStatus.Queued,
            CurrentEpoch = 0,
            CurrentAttempt = 1,
            TrainingLoss = null,
            ValidationLoss = null,
            BestValidationLoss = null,
            IsPaused = false,
            ErrorMessage = null,
            Result = null,
            Message = "Queued for retry"
        };

        // Start training again
        _ = RunTrainingAsync(state);

        return Task.FromResult(true);
    }
}
