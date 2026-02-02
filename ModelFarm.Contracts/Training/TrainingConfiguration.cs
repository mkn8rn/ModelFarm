namespace ModelFarm.Contracts.Training;

using ModelFarm.Contracts.MarketData;

/// <summary>
/// Types of training configurations that can be created.
/// </summary>
public enum ConfigurationType
{
    /// <summary>
    /// Quantitative trading strategy using ML models to predict price movements.
    /// Trains on historical OHLCV data and backtests with simulated trading.
    /// </summary>
    QuantStrategy
}

/// <summary>
/// Configuration for training a quant trading ML model.
/// Based on the parameters from the quant trading strategy notebook.
/// </summary>
public sealed record TrainingConfiguration
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// The type of configuration.
    /// </summary>
    public required ConfigurationType Type { get; init; }

    // ==================== Dataset Configuration ====================

    /// <summary>
    /// The dataset to use for training.
    /// </summary>
    public required Guid DatasetId { get; init; }

    // ==================== Model Architecture Parameters ====================

    /// <summary>
    /// Type of model to train.
    /// </summary>
    public required ModelType ModelType { get; init; }

    /// <summary>
    /// Maximum number of auto-regressive lags for feature generation.
    /// Corresponds to 'max_lags' in the notebook (default: 4).
    /// </summary>
    public int MaxLags { get; init; } = 4;

    /// <summary>
    /// Forecast horizon in time steps.
    /// Corresponds to 'forecast_horizon' in the notebook (default: 1).
    /// </summary>
    public int ForecastHorizon { get; init; } = 1;

    /// <summary>
    /// Hidden layer sizes for neural network models.
    /// </summary>
    public int[] HiddenLayerSizes { get; init; } = [64, 32];

    /// <summary>
    /// Dropout rate for regularization (0.0 to 1.0).
    /// </summary>
    public double DropoutRate { get; init; } = 0.2;

    // ==================== Training Hyperparameters ====================

    /// <summary>
    /// Learning rate for the optimizer.
    /// </summary>
    public double LearningRate { get; init; } = 0.001;

    /// <summary>
    /// Batch size for training.
    /// </summary>
    public int BatchSize { get; init; } = 32;

    /// <summary>
    /// Maximum number of training epochs.
    /// </summary>
    public int MaxEpochs { get; init; } = 10000;

    /// <summary>
    /// Early stopping patience (epochs without improvement).
    /// </summary>
    public int EarlyStoppingPatience { get; init; } = 50;

    /// <summary>
    /// If true, use early stopping based on validation loss plateau.
    /// If false, training runs for MaxEpochs regardless of validation loss.
    /// </summary>
    public bool UseEarlyStopping { get; init; } = true;

    /// <summary>
    /// Fraction of data to use for validation (0.0 to 1.0).
    /// </summary>
    public double ValidationSplit { get; init; } = 0.2;

    /// <summary>
    /// Fraction of data to use for testing (0.0 to 1.0).
    /// </summary>
    public double TestSplit { get; init; } = 0.1;

    /// <summary>
    /// Random seed for reproducibility.
    /// </summary>
    public int RandomSeed { get; init; } = 42;

    // ==================== Checkpoint Settings ====================

    /// <summary>
    /// If true, save model checkpoints during training for recovery after interruption.
    /// </summary>
    public bool SaveCheckpoints { get; init; } = true;

    /// <summary>
    /// Number of epochs between checkpoint saves. Only used if SaveCheckpoints is true.
    /// </summary>
    public int CheckpointIntervalEpochs { get; init; } = 50;

    // ==================== Retry Settings ====================

    /// <summary>
    /// If true, automatically retry training until performance requirements are met.
    /// </summary>
    public bool RetryUntilSuccess { get; init; } = false;

    /// <summary>
    /// Maximum number of retry attempts when RetryUntilSuccess is enabled.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 10;

    /// <summary>
    /// If true, shuffle training data between retries to introduce variation.
    /// </summary>
    public bool ShuffleOnRetry { get; init; } = false;

    /// <summary>
    /// If true, scale the learning rate on each retry attempt.
    /// </summary>
    public bool ScaleLearningRateOnRetry { get; init; } = false;

    /// <summary>
    /// Factor to multiply learning rate by on each retry.
    /// Values less than 1.0 decrease LR (e.g., 0.5 halves it).
    /// Values greater than 1.0 increase LR (e.g., 1.5 increases by 50%).
    /// </summary>
    public double LearningRateRetryScale { get; init; } = 0.5;

    // ==================== Inference Settings ====================

    /// <summary>
    /// If true, use GPU for model inference (predictions) when available.
    /// Recommended for larger models (MLP, LSTM) with many predictions.
    /// For small models like LinearRegression, CPU is often faster due to transfer overhead.
    /// </summary>
    public bool UseGpuForInference { get; init; } = false;

    // ==================== Performance Requirements ====================

    /// <summary>
    /// Performance thresholds the model must meet.
    /// </summary>
    public required PerformanceRequirements PerformanceRequirements { get; init; }

    // ==================== Trading Environment ====================

    /// <summary>
    /// Trading environment configuration for backtesting.
    /// </summary>
    public required TradingEnvironmentConfig TradingEnvironment { get; init; }

    // ==================== Metadata ====================

    /// <summary>
    /// When the configuration was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// When the configuration was last modified.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; init; }
}

/// <summary>
/// Types of ML models supported for quant trading.
/// </summary>
public enum ModelType
{
    /// <summary>
    /// Simple linear regression model.
    /// </summary>
    LinearRegression,

    /// <summary>
    /// Multi-layer perceptron (feedforward neural network).
    /// </summary>
    MLP,

    /// <summary>
    /// Long Short-Term Memory recurrent neural network.
    /// </summary>
    LSTM,

    /// <summary>
    /// Gated Recurrent Unit neural network.
    /// </summary>
    GRU,

    /// <summary>
    /// Transformer-based model.
    /// </summary>
    Transformer,

    /// <summary>
    /// Gradient Boosting (XGBoost-style).
    /// </summary>
    GradientBoosting,

    /// <summary>
    /// Random Forest ensemble.
    /// </summary>
    RandomForest
}

/// <summary>
/// Performance thresholds a trained model must meet.
/// </summary>
public sealed record PerformanceRequirements
{
    /// <summary>
    /// Minimum annualized Sharpe ratio required.
    /// Sharpe = (Return - RiskFreeRate) / StdDev
    /// </summary>
    public double? MinSharpeRatio { get; init; }

    /// <summary>
    /// Minimum annualized Sortino ratio required.
    /// Similar to Sharpe but only considers downside deviation.
    /// </summary>
    public double? MinSortinoRatio { get; init; }

    /// <summary>
    /// Maximum drawdown allowed as a decimal (e.g., 0.2 for 20%).
    /// </summary>
    public double? MaxDrawdown { get; init; }

    /// <summary>
    /// Minimum win rate required (0.0 to 1.0).
    /// </summary>
    public double? MinWinRate { get; init; }

    /// <summary>
    /// Minimum profit factor required (GrossProfit / GrossLoss).
    /// </summary>
    public double? MinProfitFactor { get; init; }

    /// <summary>
    /// Minimum annualized return required as a decimal.
    /// </summary>
    public double? MinAnnualizedReturn { get; init; }

    /// <summary>
    /// Maximum number of consecutive losing trades allowed.
    /// </summary>
    public int? MaxConsecutiveLosses { get; init; }

    /// <summary>
    /// Minimum number of trades required for statistical significance.
    /// </summary>
    public int MinTradeCount { get; init; } = 30;

    /// <summary>
    /// Risk-free rate for Sharpe/Sortino calculations (annualized).
    /// </summary>
    public double RiskFreeRate { get; init; } = 0.05; // 5%

    /// <summary>
    /// Number of trading days per year for annualization.
    /// 365 for crypto (24/7 markets), 252 for traditional markets.
    /// </summary>
    public int TradingDaysPerYear { get; init; } = 365;

    /// <summary>
    /// Number of trading hours per day for annualization.
    /// 24 for crypto, typically 6.5 for traditional markets.
    /// </summary>
    public int TradingHoursPerDay { get; init; } = 24;

    /// <summary>
    /// Common requirement presets.
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// Aggressive performance targets for high-frequency strategies.
        /// </summary>
        public static PerformanceRequirements Aggressive => new()
        {
            MinSharpeRatio = 2.0,
            MinSortinoRatio = 2.5,
            MaxDrawdown = 0.15,
            MinWinRate = 0.55,
            MinProfitFactor = 1.5,
            MinAnnualizedReturn = 0.30
        };

        /// <summary>
        /// Moderate performance targets.
        /// </summary>
        public static PerformanceRequirements Moderate => new()
        {
            MinSharpeRatio = 1.0,
            MaxDrawdown = 0.25,
            MinWinRate = 0.45,
            MinProfitFactor = 1.2
        };

        /// <summary>
        /// Conservative performance targets.
        /// </summary>
        public static PerformanceRequirements Conservative => new()
        {
            MinSharpeRatio = 0.5,
            MaxDrawdown = 0.35,
            MinProfitFactor = 1.0
        };

        /// <summary>
        /// No requirements (for experimentation).
        /// </summary>
        public static PerformanceRequirements None => new();
    }
}
