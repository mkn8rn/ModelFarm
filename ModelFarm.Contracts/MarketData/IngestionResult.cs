namespace ModelFarm.Contracts.MarketData;

/// <summary>
/// Summary result of a data ingestion operation.
/// </summary>
public sealed record IngestionResult
{
    public required Exchange Exchange { get; init; }
    public required string Symbol { get; init; }
    public required KlineInterval Interval { get; init; }
    public required int TotalRecords { get; init; }
    public required DateTime FirstTimestampUtc { get; init; }
    public required DateTime LastTimestampUtc { get; init; }
    public required TimeSpan TimeSpanCovered { get; init; }
    public required TimeSpan IngestionDuration { get; init; }
    public required IReadOnlyList<Kline> SampleRecords { get; init; }

    public decimal? HighestPrice { get; init; }
    public decimal? LowestPrice { get; init; }
    public decimal? TotalVolume { get; init; }
    public int? TotalTrades { get; init; }
}
