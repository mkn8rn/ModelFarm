using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ModelFarm.Application.ML;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Training;
using ModelFarm.Infrastructure.Persistence;
using ModelFarm.Infrastructure.Persistence.Entities;

namespace ModelFarm.Application.Services;



public sealed class TrainingService : ITrainingService
{
    private readonly IDatasetService _datasetService;
    private readonly IModelTrainer _modelTrainer;
    private readonly BacktestEngine _backtestEngine;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly CheckpointManager _checkpointManager;

    // Runtime state for active jobs (not persisted - only used while training is in progress)
    private static readonly ConcurrentDictionary<Guid, TrainingJobRuntimeState> _activeJobs = new();
    
    // Semaphore to limit concurrent training operations to prevent GPU/memory exhaustion
    // Default to 1 concurrent job to avoid TorchSharp ExternalException errors
    private static SemaphoreSlim _trainingSemaphore = new(1, int.MaxValue);
    private static int _maxConcurrentJobs = 1;
    private static readonly object _semaphoreLock = new();

    public TrainingService(
        IDatasetService datasetService,
        IModelTrainer modelTrainer,
        BacktestEngine backtestEngine,
        IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _datasetService = datasetService;
        _modelTrainer = modelTrainer;
        _backtestEngine = backtestEngine;
        _dbContextFactory = dbContextFactory;
        _checkpointManager = new CheckpointManager();
    }

    // ==================== Concurrency Settings ====================

    public int GetMaxConcurrentJobs() => _maxConcurrentJobs;

    public void SetMaxConcurrentJobs(int maxConcurrent)
    {
        if (maxConcurrent < 1)
            maxConcurrent = 1;
        
        lock (_semaphoreLock)
        {
            if (maxConcurrent == _maxConcurrentJobs)
                return;

            var oldMax = _maxConcurrentJobs;
            _maxConcurrentJobs = maxConcurrent;

            // Adjust semaphore capacity
            if (maxConcurrent > oldMax)
            {
                // Release additional slots
                var diff = maxConcurrent - oldMax;
                _trainingSemaphore.Release(diff);
            }
            // Note: If reducing, we can't forcibly remove slots that are in use.
            // The semaphore will naturally enforce the new limit as jobs complete.
        }
    }

    public int GetRunningJobCount()
    {
        // Count jobs that are actively training (not waiting for slot)
        return _activeJobs.Values.Count(j => !j.CancellationTokenSource.IsCancellationRequested);
    }

    // ==================== Configuration Management ====================

    public async Task<TrainingConfiguration> CreateConfigurationAsync(CreateTrainingConfigurationRequest request, CancellationToken cancellationToken = default)
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
            UseEarlyStopping = request.UseEarlyStopping,
            ValidationSplit = request.ValidationSplit,
            TestSplit = request.TestSplit,
            RandomSeed = request.RandomSeed,
            SaveCheckpoints = request.SaveCheckpoints,
            CheckpointIntervalEpochs = request.CheckpointIntervalEpochs,
            RetryUntilSuccess = request.RetryUntilSuccess,
            MaxRetryAttempts = request.MaxRetryAttempts,
            ShuffleOnRetry = request.ShuffleOnRetry,
            ScaleLearningRateOnRetry = request.ScaleLearningRateOnRetry,
            LearningRateRetryScale = request.LearningRateRetryScale,
            PerformanceRequirements = request.PerformanceRequirements,
            TradingEnvironment = request.TradingEnvironment,
            CreatedAtUtc = DateTime.UtcNow
        };

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = TrainingConfigurationEntity.FromConfiguration(config);
        db.TrainingConfigurations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return config;
    }

    public async Task<TrainingConfiguration?> GetConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.TrainingConfigurations.FindAsync([configurationId], cancellationToken);
        return entity?.ToConfiguration();
    }

    public async Task<IReadOnlyList<TrainingConfiguration>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.TrainingConfigurations
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return entities.Select(e => e.ToConfiguration()).ToList();
    }



    public async Task<TrainingConfiguration> UpdateConfigurationAsync(Guid configurationId, UpdateTrainingConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.TrainingConfigurations.FindAsync([configurationId], cancellationToken);
        if (entity is null)
            throw new KeyNotFoundException($"Configuration {configurationId} not found");

        var existing = entity.ToConfiguration();
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
            UseEarlyStopping = request.UseEarlyStopping ?? existing.UseEarlyStopping,
            ValidationSplit = request.ValidationSplit ?? existing.ValidationSplit,
            TestSplit = request.TestSplit ?? existing.TestSplit,
            RandomSeed = request.RandomSeed ?? existing.RandomSeed,
            SaveCheckpoints = request.SaveCheckpoints ?? existing.SaveCheckpoints,
            CheckpointIntervalEpochs = request.CheckpointIntervalEpochs ?? existing.CheckpointIntervalEpochs,
            RetryUntilSuccess = request.RetryUntilSuccess ?? existing.RetryUntilSuccess,
            MaxRetryAttempts = request.MaxRetryAttempts ?? existing.MaxRetryAttempts,
            ShuffleOnRetry = request.ShuffleOnRetry ?? existing.ShuffleOnRetry,
            ScaleLearningRateOnRetry = request.ScaleLearningRateOnRetry ?? existing.ScaleLearningRateOnRetry,
            LearningRateRetryScale = request.LearningRateRetryScale ?? existing.LearningRateRetryScale,
            PerformanceRequirements = request.PerformanceRequirements ?? existing.PerformanceRequirements,
            TradingEnvironment = request.TradingEnvironment ?? existing.TradingEnvironment,
            UpdatedAtUtc = DateTime.UtcNow
        };

        entity.UpdateFrom(updated);
        await db.SaveChangesAsync(cancellationToken);

        return updated;
    }

    public async Task<bool> DeleteConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.TrainingConfigurations.FindAsync([configurationId], cancellationToken);
        if (entity is null)
            return false;


        db.TrainingConfigurations.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }


    // ==================== Training Job Management ====================



    public async Task<TrainingJob> StartTrainingAsync(TrainingJobRequest request, CancellationToken cancellationToken = default)
    {
        var config = await GetConfigurationAsync(request.ConfigurationId, cancellationToken);
        if (config is null)
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

        var maxAttempts = config.RetryUntilSuccess ? config.MaxRetryAttempts : 1;

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

        // Save job to database
        await using (var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var entity = TrainingJobEntity.FromJob(job);
            db.TrainingJobs.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Create runtime state for active job
        var runtimeState = new TrainingJobRuntimeState
        {
            JobId = jobId,
            Configuration = config,
            Dataset = dataset,
            CancellationTokenSource = new CancellationTokenSource()
        };

        _activeJobs[jobId] = runtimeState;

        // Start training in background if data is ready
        if (initialStatus == TrainingJobStatus.Queued)
        {
            _ = RunTrainingAsync(runtimeState);
        }

        return job;
    }

    public async Task<TrainingJob?> GetTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // Check active jobs first for most current state
        if (_activeJobs.TryGetValue(jobId, out var runtimeState))
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var entity = await db.TrainingJobs.FindAsync([jobId], cancellationToken);
            if (entity is not null)
            {
                var job = entity.ToJob();
                // Merge runtime state (IsPaused is transient)
                return job with { IsPaused = runtimeState.IsPaused };
            }
        }



        // Fall back to database
        await using (var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var entity = await db.TrainingJobs.FindAsync([jobId], cancellationToken);
            return entity?.ToJob();
        }
    }

    public async Task<IReadOnlyList<TrainingJob>> GetAllTrainingJobsAsync(TrainingJobStatus? statusFilter = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.TrainingJobs.AsQueryable();
        
        if (statusFilter.HasValue)
            query = query.Where(j => j.Status == statusFilter.Value);
        
        var entities = await query.OrderByDescending(j => j.CreatedAtUtc).ToListAsync(cancellationToken);
        
        // Merge runtime state for active jobs
        return entities.Select(e =>
        {
            var job = e.ToJob();
            if (_activeJobs.TryGetValue(job.Id, out var runtimeState))
            {
                return job with { IsPaused = runtimeState.IsPaused };
            }
            return job;
        }).ToList();
    }

    public async Task<bool> CancelTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        // Cancel runtime if active
        if (_activeJobs.TryGetValue(jobId, out var runtimeState))
        {
            runtimeState.CancellationTokenSource.Cancel();
        }

        // Update database
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.TrainingJobs.FindAsync([jobId], cancellationToken);
        if (entity is null)
            return false;

        entity.Status = TrainingJobStatus.Cancelled;
        entity.Message = "Training cancelled by user";
        entity.CompletedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<TrainingJob>> GetJobsForConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.TrainingJobs
            .Where(j => j.ConfigurationId == configurationId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return entities.Select(e => e.ToJob()).ToList();
    }

    private async Task RunTrainingAsync(TrainingJobRuntimeState state)
    {
        var config = state.Configuration;
        var cts = state.CancellationTokenSource;
        var jobId = state.JobId;

        // Acquire semaphore to ensure only one training job runs at a time
        // This prevents TorchSharp ExternalException errors from concurrent GPU access
        await UpdateJobAsync(jobId, job => job with { Message = "Waiting for training slot..." });
        
        try
        {
            await _trainingSemaphore.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            await UpdateJobAsync(jobId, job => job with
            {
                Status = TrainingJobStatus.Cancelled,
                Message = "Training cancelled while waiting for slot",
                CompletedAtUtc = DateTime.UtcNow
            });
            _activeJobs.TryRemove(jobId, out _);
            return;
        }

        try
        {
            // Update status to preprocessing
            await UpdateJobAsync(jobId, job => job with
            {
                Status = TrainingJobStatus.Preprocessing,
                StartedAtUtc = DateTime.UtcNow,
                Message = "Loading and preparing data..."
            });

            // Load kline data
            var klines = await _datasetService.GetDatasetKlinesAsync(config.DatasetId, cts.Token);
            if (klines.Count == 0)
                throw new InvalidOperationException($"Dataset contains no data. The dataset '{state.Dataset.Name}' ({state.Dataset.Symbol}, {state.Dataset.Interval}) has 0 records in the specified date range.");

            var minRequired = config.MaxLags + config.ForecastHorizon + 10;
            if (klines.Count < minRequired)
                throw new InvalidOperationException($"Dataset has insufficient data ({klines.Count} records). Need at least {minRequired} records for MaxLags={config.MaxLags} and ForecastHorizon={config.ForecastHorizon}.");

            await UpdateJobAsync(jobId, job => job with { Message = $"Preparing features from {klines.Count} records..." });

            // Prepare features - uses streaming approach with circular buffer
            var allData = FeatureEngineering.PrepareTrainingData(klines, config.MaxLags, config.ForecastHorizon);
            
            // Release klines memory immediately after feature extraction
            klines = null!;
            GC.Collect(0, GCCollectionMode.Optimized);
            
            await UpdateJobAsync(jobId, job => job with { Message = $"Splitting {allData.Samples.Count} samples into train/val/test..." });

            // Split into train/validation/test - uses list slicing, not copying
            var (trainData, validationData, testData) = FeatureEngineering.SplitData(
                allData, 
                config.ValidationSplit, 
                config.TestSplit);

            // Clear allData reference (samples are now owned by split datasets)
            allData = null!;

            // Normalize features
            var (normalizedTrain, normStats) = FeatureEngineering.NormalizeFeatures(trainData);
            var normalizedValidation = FeatureEngineering.ApplyNormalization(validationData, normStats);
            var normalizedTest = FeatureEngineering.ApplyNormalization(testData, normStats);

            // Release unnormalized data
            trainData = null!;
            validationData = null!;
            testData = null!;
            GC.Collect(0, GCCollectionMode.Optimized);

            // Check for existing checkpoint to resume from
            TrainingCheckpoint? checkpoint = null;
            if (state.ResumeFromCheckpoint && config.SaveCheckpoints)
            {
                checkpoint = await _checkpointManager.LoadCheckpointAsync(jobId, cts.Token);
                if (checkpoint is not null)
                {
                    await UpdateJobAsync(jobId, job => job with
                    {
                        Message = $"Resuming from checkpoint at epoch {checkpoint.Epoch}..."
                    });
                }
            }

            // Retry loop
            TrainingResult? finalResult = null;
            var totalTrainingDuration = checkpoint?.TotalTrainingDuration ?? TimeSpan.Zero;
            var effectiveConfig = config;
            
            // Initialize retry attempt from saved state (for resume from checkpoint)
            state.RetryAttempt = state.InitialRetryAttempt;
            
            do
            {
                state.RetryAttempt++;
                cts.Token.ThrowIfCancellationRequested();

                // Check for pause
                while (state.IsPaused && !cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(500, cts.Token);
                }
                
                // Scale learning rate on retry if enabled (only for actual retries, not resume)
                // Don't clear checkpoint if we're resuming from a checkpoint on this attempt
                var isActualRetry = state.RetryAttempt > 1 && !state.ResumeFromCheckpoint;
                var isResumeAttempt = state.ResumeFromCheckpoint && state.RetryAttempt == state.InitialRetryAttempt + 1;
                
                if (isActualRetry && config.ScaleLearningRateOnRetry)
                {
                    effectiveConfig = effectiveConfig with
                    {
                        LearningRate = effectiveConfig.LearningRate * config.LearningRateRetryScale
                    };
                    // Clear checkpoint since we're starting fresh with new LR
                    checkpoint = null;
                }

                // Shuffle training data on retry if enabled (only for actual retries, not resume)
                var trainDataForRun = normalizedTrain;
                if (isActualRetry && config.ShuffleOnRetry)
                {
                    trainDataForRun = ShuffleTrainingData(normalizedTrain, state.RetryAttempt);
                    // Clear checkpoint since data order changed
                    checkpoint = null;
                }

                var maxAttemptsDisplay = config.RetryUntilSuccess ? config.MaxRetryAttempts : 1;
                var startEpoch = checkpoint?.Epoch ?? 0;
                await UpdateJobAsync(jobId, job => job with
                {
                    Status = TrainingJobStatus.Training,
                    CurrentEpoch = startEpoch,
                    CurrentAttempt = state.RetryAttempt,
                    MaxAttempts = maxAttemptsDisplay,
                    Message = checkpoint is not null 
                        ? $"Resuming training from epoch {startEpoch}..." 
                        : $"Training on {trainDataForRun.Samples.Count} samples..."
                });

                // Create progress reporter that updates database periodically
                var lastUpdate = DateTime.UtcNow;
                var progress = new Progress<TrainingProgress>(async p =>
                {
                    // Throttle database updates to avoid excessive writes
                    var now = DateTime.UtcNow;
                    if ((now - lastUpdate).TotalMilliseconds >= 500)
                    {
                        lastUpdate = now;
                        // Use optimized single-statement update (no SELECT)
                        await UpdateJobProgressAsync(jobId, 
                            p.CurrentEpoch, 
                            p.TrainingLoss, 
                            p.ValidationLoss, 
                            p.BestValidationLoss, 
                            p.EpochsSinceImprovement, 
                            p.Message);
                    }
                });

                // Callback when checkpoint is saved (only if checkpoints enabled)
                Func<int, double, Task>? onCheckpointSaved = config.SaveCheckpoints
                    ? async (epoch, bestLoss) => await UpdateJobWithCheckpointAsync(jobId, epoch)
                    : null;

                // Apply early stopping setting from config
                var runConfig = config.UseEarlyStopping 
                    ? effectiveConfig 
                    : effectiveConfig with { EarlyStoppingPatience = int.MaxValue };

                // Determine checkpoint interval (0 = disabled)
                var checkpointInterval = config.SaveCheckpoints ? config.CheckpointIntervalEpochs : 0;

                // Train the model with checkpoint support
                var trainingResult = await _modelTrainer.TrainWithCheckpointsAsync(
                    trainDataForRun,
                    normalizedValidation,
                    runConfig,
                    _checkpointManager,
                    jobId,
                    checkpoint,
                    normStats,
                    checkpointInterval,
                    progress,
                    onCheckpointSaved,
                    () => state.IsPaused,  // Pass pause check function
                    cts.Token);

                // Clear checkpoint after first attempt so retries start fresh
                checkpoint = null;
                totalTrainingDuration = trainingResult.TrainingDuration;
                
                // Clear resume flag so subsequent retries are treated as actual retries
                state.ResumeFromCheckpoint = false;

                await UpdateJobAsync(jobId, job => job with
                {
                    Status = TrainingJobStatus.Backtesting,
                    CurrentEpoch = trainingResult.EpochsTrained,
                    Message = "Running backtest on test data..."
                });

                // Evaluate on test set
                var evalResult = await _modelTrainer.EvaluateAsync(trainingResult.Model, normalizedTest, cts.Token);

                // Dispose the trained model after evaluation - we only need the metrics
                trainingResult.Model.Dispose();

                // Run backtest
                var annualizationFactor = CalculateAnnualizationFactor(state.Dataset.Interval);
                var backtestResult = _backtestEngine.RunBacktest(
                    evalResult.Predictions, 
                    config.TradingEnvironment,
                    annualizationFactor);

                // Clear predictions after backtest to free memory
                evalResult = evalResult with { Predictions = [] };

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
                if (meetsRequirements || !config.RetryUntilSuccess)
                {
                    break;
                }

                // Check if we've exhausted retries
                if (state.RetryAttempt >= config.MaxRetryAttempts)
                {
                    await UpdateJobAsync(jobId, job => job with
                    {
                        Message = $"Max retries ({config.MaxRetryAttempts}) reached without meeting requirements"
                    });
                    break;
                }

                // Brief pause before retry
                await UpdateJobAsync(jobId, job => job with
                {
                    Message = $"Attempt {state.RetryAttempt} did not meet requirements. Retrying..."
                });
                await Task.Delay(100, cts.Token);

            } while (config.RetryUntilSuccess && state.RetryAttempt < config.MaxRetryAttempts);

            var completionMsg = finalResult!.MeetsRequirements 
                ? $"Training completed - meets requirements" 
                : $"Training completed - does not meet requirements";
            
            if (state.RetryAttempt > 1)
            {
                completionMsg += $" (after {state.RetryAttempt} attempts)";
            }

            await UpdateJobAsync(jobId, job => job with
            {
                Status = TrainingJobStatus.Completed,
                CurrentEpoch = finalResult.EpochsTrained,
                Message = completionMsg,
                CompletedAtUtc = DateTime.UtcNow,
                Result = finalResult
            });
        }
        catch (OperationCanceledException)
        {
            await UpdateJobAsync(jobId, job => job with
            {
                Status = TrainingJobStatus.Cancelled,
                Message = "Training cancelled",
                CompletedAtUtc = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            await UpdateJobAsync(jobId, job => job with
            {
                Status = TrainingJobStatus.Failed,
                Message = $"Training failed: {ex.Message}",
                ErrorMessage = ex.Message,
                CompletedAtUtc = DateTime.UtcNow
            });
        }
        finally
        {
            // Release semaphore to allow next training job to run
            _trainingSemaphore.Release();
            
            // Clean up runtime state when job completes
            _activeJobs.TryRemove(jobId, out _);
        }
    }

    // Helper method to update job in database - uses ExecuteUpdateAsync for single-statement update
    private async Task UpdateJobAsync(Guid jobId, Func<TrainingJob, TrainingJob> updateFunc)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var entity = await db.TrainingJobs.FindAsync(jobId);
        if (entity is not null)
        {
            var job = entity.ToJob();
            var updatedJob = updateFunc(job);
            entity.UpdateFrom(updatedJob);
            await db.SaveChangesAsync();
        }
    }

    // Optimized: Update only progress fields with single UPDATE statement (no SELECT)
    private async Task UpdateJobProgressAsync(Guid jobId, int currentEpoch, double trainingLoss, double validationLoss, double bestValidationLoss, int epochsSinceImprovement, string message)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        await db.TrainingJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.CurrentEpoch, currentEpoch)
                .SetProperty(j => j.TrainingLoss, trainingLoss)
                .SetProperty(j => j.ValidationLoss, validationLoss)
                .SetProperty(j => j.BestValidationLoss, bestValidationLoss)
                .SetProperty(j => j.EpochsSinceImprovement, epochsSinceImprovement)
                .SetProperty(j => j.Message, message.Length > 1000 ? message[..997] + "..." : message));
    }

    // Optimized: Update status with single UPDATE statement (no SELECT)
    private async Task UpdateJobStatusAsync(Guid jobId, TrainingJobStatus status, string message, DateTime? startedAtUtc = null, DateTime? completedAtUtc = null, string? errorMessage = null, string? resultJson = null)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        await db.TrainingJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.Status, status)
                .SetProperty(j => j.Message, message.Length > 1000 ? message[..997] + "..." : message)
                .SetProperty(j => j.StartedAtUtc, startedAtUtc)
                .SetProperty(j => j.CompletedAtUtc, completedAtUtc)
                .SetProperty(j => j.ErrorMessage, errorMessage != null && errorMessage.Length > 1000 ? errorMessage[..997] + "..." : errorMessage)
                .SetProperty(j => j.ResultJson, resultJson));
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

    /// <summary>
    /// Runtime state for active training jobs (not persisted).
    /// Contains transient data like CancellationTokenSource and pause state.
    /// </summary>
    private sealed class TrainingJobRuntimeState
    {
        public required Guid JobId { get; init; }
        public required TrainingConfiguration Configuration { get; init; }
        public required DatasetDefinition Dataset { get; init; }
        public required CancellationTokenSource CancellationTokenSource { get; init; }
        public int RetryAttempt { get; set; } = 0;
        public int InitialRetryAttempt { get; init; } = 0;  // For resume: the attempt we're continuing
        public bool IsPaused { get; set; } = false;
        public bool ResumeFromCheckpoint { get; set; } = false;  // Set to false after first training completes
    }

    // Helper method to update job checkpoint status - uses ExecuteUpdateAsync (no SELECT)
    private async Task UpdateJobWithCheckpointAsync(Guid jobId, int epoch)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();
        await db.TrainingJobs
            .Where(j => j.Id == jobId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(j => j.HasCheckpoint, true)
                .SetProperty(j => j.LastCheckpointAtUtc, DateTime.UtcNow));
    }

    public async Task<bool> PauseTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_activeJobs.TryGetValue(jobId, out var runtimeState))
            return false;

        // Check job status from database
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.TrainingJobs.FindAsync([jobId], cancellationToken);
        if (entity is null)
            return false;

        if (entity.Status is not (TrainingJobStatus.Training or TrainingJobStatus.Backtesting or TrainingJobStatus.Preprocessing))
            return false;

        runtimeState.IsPaused = true;
        entity.IsPaused = true;
        entity.Message = "Paused";
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> ResumePausedJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (!_activeJobs.TryGetValue(jobId, out var runtimeState))
            return false;

        if (!runtimeState.IsPaused)
            return false;

        runtimeState.IsPaused = false;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.TrainingJobs.FindAsync([jobId], cancellationToken);
        if (entity is not null)
        {
            entity.IsPaused = false;
            entity.Message = "Resumed";
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> RetryTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.TrainingJobs.FindAsync([jobId], cancellationToken);
        if (entity is null)
            return false;

        // Can only retry failed or completed jobs
        if (entity.Status is not (TrainingJobStatus.Failed or TrainingJobStatus.Completed or TrainingJobStatus.Cancelled))
            return false;

        // Get configuration
        var config = await GetConfigurationAsync(entity.ConfigurationId, cancellationToken);
        if (config is null)
            return false;


        // Get dataset
        var dataset = await _datasetService.GetDatasetAsync(config.DatasetId, cancellationToken);
        if (dataset is null)
            return false;

        // Delete any existing checkpoint (retry starts fresh)
        _checkpointManager.DeleteCheckpoint(jobId);

        // Reset job state in database
        entity.Status = TrainingJobStatus.Queued;
        entity.CurrentEpoch = 0;
        entity.CurrentAttempt = 1;
        entity.TrainingLoss = null;
        entity.ValidationLoss = null;
        entity.BestValidationLoss = null;
        entity.IsPaused = false;
        entity.ErrorMessage = null;
        entity.ResultJson = null;
        entity.HasCheckpoint = false;
        entity.LastCheckpointAtUtc = null;
        entity.Message = "Queued for retry";
        entity.StartedAtUtc = null;
        entity.CompletedAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);

        // Create new runtime state (not resuming from checkpoint)
        var runtimeState = new TrainingJobRuntimeState
        {
            JobId = jobId,
            Configuration = config,
            Dataset = dataset,
            CancellationTokenSource = new CancellationTokenSource(),
            ResumeFromCheckpoint = false
        };

        _activeJobs[jobId] = runtimeState;

        // Start training again
        _ = RunTrainingAsync(runtimeState);

        return true;
    }

    /// <summary>
    /// Resumes a training job from its last checkpoint.
    /// </summary>
    public async Task<bool> ResumeTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.TrainingJobs.FindAsync([jobId], cancellationToken);
        if (entity is null)
            return false;

        // Can only resume failed jobs that have checkpoints
        if (entity.Status is not TrainingJobStatus.Failed)
            return false;

        if (!entity.HasCheckpoint || !_checkpointManager.CheckpointExists(jobId))
            return false;

        // Get configuration
        var config = await GetConfigurationAsync(entity.ConfigurationId, cancellationToken);
        if (config is null)
            return false;

        // Get dataset
        var dataset = await _datasetService.GetDatasetAsync(config.DatasetId, cancellationToken);
        if (dataset is null)
            return false;

        // Preserve the current attempt count for resume
        var currentAttempt = entity.CurrentAttempt;

        // Update job state - keep current epoch and attempt since we're resuming
        entity.Status = TrainingJobStatus.Queued;
        entity.IsPaused = false;
        entity.ErrorMessage = null;
        entity.ResultJson = null;
        entity.Message = $"Queued for resume from epoch {entity.CurrentEpoch} (attempt {currentAttempt})";
        entity.CompletedAtUtc = null;
        await db.SaveChangesAsync(cancellationToken);

        // Create runtime state with resume flag and preserved attempt count
        var runtimeState = new TrainingJobRuntimeState
        {
            JobId = jobId,
            Configuration = config,
            Dataset = dataset,
            CancellationTokenSource = new CancellationTokenSource(),
            ResumeFromCheckpoint = true,
            InitialRetryAttempt = currentAttempt - 1  // -1 because it will be incremented at loop start
        };

        _activeJobs[jobId] = runtimeState;

        // Start training (will resume from checkpoint)
        _ = RunTrainingAsync(runtimeState);

        return true;
    }
}
