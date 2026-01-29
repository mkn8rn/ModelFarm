namespace ModelFarm.Contracts.MarketData;

/// <summary>
/// Supported exchanges for market data.
/// </summary>
public enum Exchange
{
    Binance
}

public static class ExchangeExtensions
{
    public static string ToDisplayString(this Exchange exchange) => exchange switch
    {
        Exchange.Binance => "Binance",
        _ => exchange.ToString()
    };
}
