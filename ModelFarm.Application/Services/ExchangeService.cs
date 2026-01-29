using System.Collections.Concurrent;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Infrastructure.MarketData;

namespace ModelFarm.Application.Services;

public sealed class ExchangeService : IExchangeService
{
    private readonly IMarketDataProviderFactory _providerFactory;
    private readonly ConcurrentDictionary<Exchange, Task<IReadOnlyList<TradingSymbol>>> _symbolCache = new();

    public ExchangeService(IMarketDataProviderFactory providerFactory)
    {
        _providerFactory = providerFactory;
    }

    public IEnumerable<Exchange> GetSupportedExchanges() => _providerFactory.SupportedExchanges;

    public async Task<IReadOnlyList<TradingSymbol>> GetSymbolsAsync(Exchange exchange, CancellationToken cancellationToken = default)
    {
        // Use GetOrAdd to ensure only one fetch per exchange, even with concurrent requests.
        // Don't pass cancellationToken to the fetch - we want to complete and cache the result
        // even if the original requester navigates away.
        var task = _symbolCache.GetOrAdd(exchange, ex =>
        {
            var provider = _providerFactory.GetProvider(ex);
            return provider.GetSymbolsAsync(CancellationToken.None);
        });

        try
        {
            return await task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // If this specific caller was cancelled but the underlying task is still running,
            // just rethrow - the task will complete and be cached for the next caller
            throw;
        }
        catch
        {
            // If the underlying task failed, remove it from cache so next caller can retry
            _symbolCache.TryRemove(exchange, out _);
            throw;
        }
    }
}
