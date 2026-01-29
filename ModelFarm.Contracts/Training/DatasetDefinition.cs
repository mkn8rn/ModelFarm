namespace ModelFarm.Contracts.Training;

using ModelFarm.Contracts.MarketData;

/// <summary>
/// Types of datasets that can be created.
/// </summary>
public enum DatasetType
{
    /// <summary>
    /// Historical OHLCV candlestick data from cryptocurrency exchanges.
    /// Used for training time-series prediction models.
    /// </summary>
    ExchangeHistory
}

/// <summary>
/// Defines a dataset for training ML models.
/// </summary>
public sealed record DatasetDefinition
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }

    /// <summary>
    /// The type of dataset.
    /// </summary>
    public required DatasetType Type { get; init; }

    /// <summary>
    /// The exchange to fetch data from.
    /// </summary>
    public required Exchange Exchange { get; init; }

    /// <summary>
    /// Trading pair symbol (e.g., "BTCUSDT").
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Time interval for the candlestick/kline data.
    /// </summary>
    public required KlineInterval Interval { get; init; }

    /// <summary>
    /// Start of the data range (UTC).
    /// </summary>
    public required DateTime StartTimeUtc { get; init; }

    /// <summary>
    /// End of the data range (UTC).
    /// </summary>
    public required DateTime EndTimeUtc { get; init; }

    /// <summary>
    /// Current status of the dataset.
    /// </summary>
    public required DatasetStatus Status { get; init; }

    /// <summary>
    /// Number of records in the dataset (if available).
    /// </summary>
    public int? RecordCount { get; init; }

    /// <summary>
    /// When the dataset was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// When the dataset was last updated.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; init; }

    /// <summary>
    /// Associated ingestion operation ID if data is being downloaded.
    /// </summary>
    public Guid? IngestionOperationId { get; init; }
}

/// <summary>
/// Status of a dataset.
/// </summary>
public enum DatasetStatus
{
    /// <summary>
    /// Dataset is defined but data hasn't been downloaded yet.
    /// </summary>
    Pending,

    /// <summary>
    /// Data is currently being downloaded.
    /// </summary>
    Downloading,

    /// <summary>
    /// Dataset is ready for use in training.
    /// </summary>
    Ready,

    /// <summary>
    /// Data download or validation failed.
    /// </summary>
    Failed
}
