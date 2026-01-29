namespace ModelFarm.Contracts.Training;

/// <summary>
/// Request to create a new training job.
/// </summary>
public sealed record TrainingJobRequest
{
    /// <summary>
    /// Training configuration ID to use.
    /// </summary>
    public required Guid ConfigurationId { get; init; }

    /// <summary>
    /// Optional name override for this job.
    /// </summary>
    public string? JobName { get; init; }

    /// <summary>
    /// Optional hyperparameter overrides for this run.
    /// </summary>
    public HyperparameterOverrides? Overrides { get; init; }

    /// <summary>
    /// Execution options for the training job.
    /// </summary>
    public TrainingExecutionOptions ExecutionOptions { get; init; } = new();
}

/// <summary>
/// Options controlling how a training job executes.
/// </summary>
public sealed record TrainingExecutionOptions
{
    /// <summary>
    /// If true, automatically retry training until performance requirements are met.
    /// </summary>
    public bool RetryUntilSuccess { get; init; } = false;

    /// <summary>
    /// Maximum number of retry attempts when RetryUntilSuccess is enabled.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 10;

    /// <summary>
    /// If true, use early stopping based on validation loss plateau.
    /// </summary>
    public bool UseEarlyStopping { get; init; } = true;

    /// <summary>
    /// If true, shuffle training data between retries to introduce variation.
    /// </summary>
    public bool ShuffleOnRetry { get; init; } = false;

    /// <summary>
    /// If true, adjust learning rate on retry (reduce by factor).
    /// </summary>
    public bool AdjustLearningRateOnRetry { get; init; } = false;

    /// <summary>
    /// Factor to multiply learning rate by on each retry (e.g., 0.5 to halve it).
    /// </summary>
    public double LearningRateRetryFactor { get; init; } = 0.5;
}

/// <summary>
/// Hyperparameter overrides for a specific training run.
/// </summary>
public sealed record HyperparameterOverrides
{
    public double? LearningRate { get; init; }
    public int? BatchSize { get; init; }
    public int? MaxEpochs { get; init; }
    public int? MaxLags { get; init; }
    public double? DropoutRate { get; init; }
}

/// <summary>
/// Represents a training job and its current state.
/// </summary>
public sealed record TrainingJob
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid ConfigurationId { get; init; }
    public required TrainingJobStatus Status { get; init; }

    /// <summary>
    /// Current epoch (if training is in progress).
    /// </summary>
    public int CurrentEpoch { get; init; }

    /// <summary>
    /// Total epochs to train.
    /// </summary>
    public int TotalEpochs { get; init; }

    /// <summary>
    /// Current training loss.
    /// </summary>
    public double? TrainingLoss { get; init; }

    /// <summary>
    /// Current validation loss.
    /// </summary>
    public double? ValidationLoss { get; init; }

    /// <summary>
    /// Best validation loss achieved so far.
    /// </summary>
    public double? BestValidationLoss { get; init; }

    /// <summary>
    /// Epochs since last improvement (for early stopping).
    /// </summary>
    public int EpochsSinceImprovement { get; init; }

    /// <summary>
    /// Current retry attempt (1 = first try).
    /// </summary>
    public int CurrentAttempt { get; init; } = 1;

    /// <summary>
    /// Maximum retry attempts allowed.
    /// </summary>
    public int MaxAttempts { get; init; } = 1;

    /// <summary>
    /// Whether the job is currently paused.
    /// </summary>
    public bool IsPaused { get; init; } = false;

    /// <summary>
    /// Progress message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// When the job was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// When training started.
    /// </summary>
    public DateTime? StartedAtUtc { get; init; }

    /// <summary>
    /// When training completed.
    /// </summary>
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>
    /// Error message if failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Final training results.
    /// </summary>
    public TrainingResult? Result { get; init; }

    /// <summary>
    /// Progress percentage.
    /// </summary>
    public double ProgressPercent => TotalEpochs > 0
        ? Math.Min(100, (double)CurrentEpoch / TotalEpochs * 100)
        : 0;
}

/// <summary>
/// Status of a training job.
/// </summary>
public enum TrainingJobStatus
{
    /// <summary>
    /// Job is queued and waiting to start.
    /// </summary>
    Queued,

    /// <summary>
    /// Waiting for dataset to be ready.
    /// </summary>
    WaitingForData,

    /// <summary>
    /// Preprocessing data.
    /// </summary>
    Preprocessing,

    /// <summary>
    /// Model is currently training.
    /// </summary>
    Training,

    /// <summary>
    /// Running backtest on test data.
    /// </summary>
    Backtesting,

    /// <summary>
    /// Training completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Training failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Training was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Results from a completed training job.
/// </summary>
public sealed record TrainingResult
{
    /// <summary>
    /// Final training loss.
    /// </summary>
    public required double FinalTrainingLoss { get; init; }

    /// <summary>
    /// Final validation loss.
    /// </summary>
    public required double FinalValidationLoss { get; init; }

    /// <summary>
    /// Test set loss.
    /// </summary>
    public required double TestLoss { get; init; }

    /// <summary>
    /// Number of epochs trained.
    /// </summary>
    public required int EpochsTrained { get; init; }

    /// <summary>
    /// Whether early stopping was triggered.
    /// </summary>
    public required bool EarlyStoppedTriggered { get; init; }

    /// <summary>
    /// Total training time.
    /// </summary>
    public required TimeSpan TrainingDuration { get; init; }

    /// <summary>
    /// Backtest performance metrics.
    /// </summary>
    public required BacktestMetrics BacktestMetrics { get; init; }

    /// <summary>
    /// Whether the model meets performance requirements.
    /// </summary>
    public required bool MeetsRequirements { get; init; }

    /// <summary>
    /// Number of retry attempts (1 = first try, >1 means retries occurred).
    /// </summary>
    public int RetryAttempts { get; init; } = 1;

    /// <summary>
    /// Path to saved model weights.
    /// </summary>
    public string? ModelPath { get; init; }
}

/// <summary>
/// Performance metrics from backtesting.
/// </summary>
public sealed record BacktestMetrics
{
    /// <summary>
    /// Total return as a decimal (e.g., 0.25 for 25%).
    /// </summary>
    public required double TotalReturn { get; init; }

    /// <summary>
    /// Annualized return.
    /// </summary>
    public required double AnnualizedReturn { get; init; }

    /// <summary>
    /// Annualized Sharpe ratio.
    /// </summary>
    public required double SharpeRatio { get; init; }

    /// <summary>
    /// Annualized Sortino ratio.
    /// </summary>
    public required double SortinoRatio { get; init; }

    /// <summary>
    /// Maximum drawdown as a decimal.
    /// </summary>
    public required double MaxDrawdown { get; init; }

    /// <summary>
    /// Calmar ratio (Annualized Return / Max Drawdown).
    /// </summary>
    public required double CalmarRatio { get; init; }

    /// <summary>
    /// Win rate (winning trades / total trades).
    /// </summary>
    public required double WinRate { get; init; }

    /// <summary>
    /// Profit factor (gross profit / gross loss).
    /// </summary>
    public required double ProfitFactor { get; init; }

    /// <summary>
    /// Total number of trades.
    /// </summary>
    public required int TotalTrades { get; init; }

    /// <summary>
    /// Number of winning trades.
    /// </summary>
    public required int WinningTrades { get; init; }

    /// <summary>
    /// Number of losing trades.
    /// </summary>
    public required int LosingTrades { get; init; }

    /// <summary>
    /// Average profit per winning trade.
    /// </summary>
    public required double AverageWin { get; init; }

    /// <summary>
    /// Average loss per losing trade.
    /// </summary>
    public required double AverageLoss { get; init; }

    /// <summary>
    /// Maximum consecutive winning trades.
    /// </summary>
    public required int MaxConsecutiveWins { get; init; }

    /// <summary>
    /// Maximum consecutive losing trades.
    /// </summary>
    public required int MaxConsecutiveLosses { get; init; }

    /// <summary>
    /// Average holding period per trade.
    /// </summary>
    public required TimeSpan AverageHoldingPeriod { get; init; }

    /// <summary>
    /// Total fees paid.
    /// </summary>
    public required decimal TotalFeesPaid { get; init; }

    /// <summary>
    /// Total slippage cost.
    /// </summary>
    public required decimal TotalSlippageCost { get; init; }

    /// <summary>
    /// Final portfolio value.
    /// </summary>
    public required decimal FinalPortfolioValue { get; init; }

    /// <summary>
    /// Backtest period start.
    /// </summary>
    public required DateTime BacktestStartUtc { get; init; }

    /// <summary>
    /// Backtest period end.
    /// </summary>
    public required DateTime BacktestEndUtc { get; init; }
}
