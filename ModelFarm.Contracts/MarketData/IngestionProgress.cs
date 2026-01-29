namespace ModelFarm.Contracts.MarketData;

/// <summary>
/// Progress information for an ongoing ingestion operation.
/// </summary>
public sealed record IngestionProgress
{
    public required Guid OperationId { get; init; }
    public required IngestionStatus Status { get; init; }
    public required int RecordsFetched { get; init; }
    public required int EstimatedTotalRecords { get; init; }
    public required string Message { get; init; }
    public DateTime? StartedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public IngestionResult? Result { get; init; }
    public string? ErrorMessage { get; init; }

    public double PercentComplete => EstimatedTotalRecords > 0 
        ? Math.Min(100, (double)RecordsFetched / EstimatedTotalRecords * 100) 
        : 0;
}

public enum IngestionStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed
}
