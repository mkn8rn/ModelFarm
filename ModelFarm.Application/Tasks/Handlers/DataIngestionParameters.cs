using ModelFarm.Contracts.MarketData;

namespace ModelFarm.Application.Tasks.Handlers;

/// <summary>
/// Parameters for a data ingestion task.
/// </summary>
public sealed record DataIngestionParameters : TaskParameters
{
    public required Exchange Exchange { get; init; }
    public required string Symbol { get; init; }
    public required KlineInterval Interval { get; init; }
    public required DateTime StartTimeUtc { get; init; }
    public required DateTime EndTimeUtc { get; init; }
}

/// <summary>
/// Result of a data ingestion task.
/// </summary>
public sealed record DataIngestionResult : TaskResult
{
    public required int TotalRecords { get; init; }
    public required DateTime FirstTimestampUtc { get; init; }
    public required DateTime LastTimestampUtc { get; init; }
    public required TimeSpan Duration { get; init; }
    
    // Statistics
    public decimal? HighestPrice { get; init; }
    public decimal? LowestPrice { get; init; }
    public decimal? TotalVolume { get; init; }
    public int? TotalTrades { get; init; }
    
    // Sample records (first 10)
    public List<KlineSample>? SampleRecords { get; init; }
}

/// <summary>
/// Lightweight kline sample for results display.
/// </summary>
public sealed record KlineSample
{
    public required DateTime OpenTimeUtc { get; init; }
    public required decimal Open { get; init; }
    public required decimal High { get; init; }
    public required decimal Low { get; init; }
    public required decimal Close { get; init; }
    public required decimal Volume { get; init; }
}
