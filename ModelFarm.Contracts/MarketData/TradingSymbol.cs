namespace ModelFarm.Contracts.MarketData;

/// <summary>
/// Represents a trading symbol/pair available on an exchange.
/// </summary>
public sealed record TradingSymbol
{
    public required string Symbol { get; init; }
    public required string BaseAsset { get; init; }
    public required string QuoteAsset { get; init; }
    public string DisplayName => $"{BaseAsset}/{QuoteAsset}";
}
