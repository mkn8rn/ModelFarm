namespace ModelFarm.Contracts.MarketData;

/// <summary>
/// Request to ingest historical market data.
/// </summary>
public sealed record IngestionRequest
{
    public required Exchange Exchange { get; init; }
    public required string Symbol { get; init; }
    public required KlineInterval Interval { get; init; }
    public required DateTime StartTimeUtc { get; init; }
    public required DateTime EndTimeUtc { get; init; }
}

/// <summary>
/// Supported kline intervals.
/// </summary>
public enum KlineInterval
{
    OneMinute,
    FiveMinutes,
    FifteenMinutes,
    OneHour,
    FourHours,
    OneDay
}

public static class KlineIntervalExtensions
{
    public static string ToApiString(this KlineInterval interval) => interval switch
    {
        KlineInterval.OneMinute => "1m",
        KlineInterval.FiveMinutes => "5m",
        KlineInterval.FifteenMinutes => "15m",
        KlineInterval.OneHour => "1h",
        KlineInterval.FourHours => "4h",
        KlineInterval.OneDay => "1d",
        _ => throw new ArgumentOutOfRangeException(nameof(interval))
    };

    public static string ToDisplayString(this KlineInterval interval) => interval switch
    {
        KlineInterval.OneMinute => "1m",
        KlineInterval.FiveMinutes => "5m",
        KlineInterval.FifteenMinutes => "15m",
        KlineInterval.OneHour => "1h",
        KlineInterval.FourHours => "4h",
        KlineInterval.OneDay => "1d",
        _ => throw new ArgumentOutOfRangeException(nameof(interval))
    };
}
