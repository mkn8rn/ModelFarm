using ModelFarm.Contracts.MarketData;

namespace ModelFarm.Application.Services;

public interface IExchangeService
{
    IEnumerable<Exchange> GetSupportedExchanges();
    Task<IReadOnlyList<TradingSymbol>> GetSymbolsAsync(Exchange exchange, CancellationToken cancellationToken = default);
}
