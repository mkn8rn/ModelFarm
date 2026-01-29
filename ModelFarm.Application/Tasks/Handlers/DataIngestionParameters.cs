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
}
