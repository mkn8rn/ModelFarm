using ModelFarm.Contracts.MarketData;

namespace ModelFarm.Infrastructure.MarketData;

/// <summary>
/// Interface for retrieving market data from an exchange.
/// </summary>
public interface IMarketDataProvider
{
    /// <summary>
    /// The exchange this provider fetches data from.
    /// </summary>
    Exchange Exchange { get; }

    /// <summary>
    /// Fetches all available trading symbols from the exchange.
    /// </summary>
    Task<IReadOnlyList<TradingSymbol>> GetSymbolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches historical kline data for the specified parameters.
    /// </summary>
    IAsyncEnumerable<Kline> GetKlinesAsync(
        string symbol,
        KlineInterval interval,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}
