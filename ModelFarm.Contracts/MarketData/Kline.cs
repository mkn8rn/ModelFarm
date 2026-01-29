namespace ModelFarm.Contracts.MarketData;

/// <summary>
/// Represents a single candlestick (kline) record from market data.
/// </summary>
public sealed record Kline
{
    public required long OpenTime { get; init; }
    public required decimal Open { get; init; }
    public required decimal High { get; init; }
    public required decimal Low { get; init; }
    public required decimal Close { get; init; }
    public required decimal Volume { get; init; }
    public required long CloseTime { get; init; }
    public required decimal QuoteAssetVolume { get; init; }
    public required int NumberOfTrades { get; init; }

    public DateTime OpenTimeUtc => DateTimeOffset.FromUnixTimeMilliseconds(OpenTime).UtcDateTime;
    public DateTime CloseTimeUtc => DateTimeOffset.FromUnixTimeMilliseconds(CloseTime).UtcDateTime;
}
