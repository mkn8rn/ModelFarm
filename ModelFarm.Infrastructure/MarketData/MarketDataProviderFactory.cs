using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Infrastructure.Persistence;

namespace ModelFarm.Infrastructure.MarketData;

/// <summary>
/// Factory for resolving market data providers by exchange.
/// </summary>
public interface IMarketDataProviderFactory
{
    IMarketDataProvider GetProvider(Exchange exchange);
    IEnumerable<Exchange> SupportedExchanges { get; }
}

public sealed class MarketDataProviderFactory : IMarketDataProviderFactory
{
    private readonly BinanceMarketDataProvider _binance;
    private readonly IDbContextFactory<DataDbContext> _dbFactory;

    public MarketDataProviderFactory(
        BinanceMarketDataProvider binance,
        IDbContextFactory<DataDbContext> dbFactory)
    {
        _binance = binance;
        _dbFactory = dbFactory;
    }

    public IMarketDataProvider GetProvider(Exchange exchange)
    {
        var inner = exchange switch
        {
            Exchange.Binance => _binance,
            _ => throw new ArgumentException($"No provider for exchange: {exchange}", nameof(exchange))
        };

        return new CachedMarketDataProvider(inner, _dbFactory);
    }

    public IEnumerable<Exchange> SupportedExchanges => [Exchange.Binance];
}
