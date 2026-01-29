using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ModelFarm.Contracts.MarketData;

namespace ModelFarm.Infrastructure.MarketData;

/// <summary>
/// Binance implementation of the market data provider.
/// </summary>
public sealed class BinanceMarketDataProvider : IMarketDataProvider
{
    private readonly HttpClient _httpClient;
    private const int MaxKlinesPerRequest = 1000;
    private const int RateLimitDelayMs = 100;

    public BinanceMarketDataProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Exchange Exchange => Exchange.Binance;

    public async Task<IReadOnlyList<TradingSymbol>> GetSymbolsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/api/v3/exchangeInfo", cancellationToken);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), 
            cancellationToken: cancellationToken);

        var symbols = new List<TradingSymbol>();
        var symbolsArray = doc.RootElement.GetProperty("symbols");

        foreach (var symbolElement in symbolsArray.EnumerateArray())
        {
            var status = symbolElement.GetProperty("status").GetString();
            if (status != "TRADING")
                continue;

            var symbol = symbolElement.GetProperty("symbol").GetString()!;
            var baseAsset = symbolElement.GetProperty("baseAsset").GetString()!;
            var quoteAsset = symbolElement.GetProperty("quoteAsset").GetString()!;

            symbols.Add(new TradingSymbol
            {
                Symbol = symbol,
                BaseAsset = baseAsset,
                QuoteAsset = quoteAsset
            });
        }

        return symbols
            .OrderBy(s => s.QuoteAsset)
            .ThenBy(s => s.BaseAsset)
            .ToList();
    }

    public async IAsyncEnumerable<Kline> GetKlinesAsync(
        string symbol,
        KlineInterval interval,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        IProgress<int>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var startMs = new DateTimeOffset(startTimeUtc, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var endMs = new DateTimeOffset(endTimeUtc, TimeSpan.Zero).ToUnixTimeMilliseconds();
        var intervalString = interval.ToApiString();
        var totalFetched = 0;

        while (startMs < endMs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var url = $"/api/v3/klines?symbol={symbol}&interval={intervalString}&startTime={startMs}&endTime={endMs}&limit={MaxKlinesPerRequest}";
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var rawKlines = await response.Content.ReadFromJsonAsync<JsonElement[]>(cancellationToken: cancellationToken);
            
            if (rawKlines is null || rawKlines.Length == 0)
                break;

            foreach (var rawKline in rawKlines)
            {
                var kline = ParseKline(rawKline);
                yield return kline;
                totalFetched++;
            }

            // Move start to after the last kline's close time for next batch
            startMs = ParseKline(rawKlines[^1]).CloseTime + 1;
            
            progress?.Report(totalFetched);

            if (rawKlines.Length < MaxKlinesPerRequest)
                break;

            await Task.Delay(RateLimitDelayMs, cancellationToken);
        }
    }

    private static Kline ParseKline(JsonElement element)
    {
        return new Kline
        {
            OpenTime = element[0].GetInt64(),
            Open = decimal.Parse(element[1].GetString()!),
            High = decimal.Parse(element[2].GetString()!),
            Low = decimal.Parse(element[3].GetString()!),
            Close = decimal.Parse(element[4].GetString()!),
            Volume = decimal.Parse(element[5].GetString()!),
            CloseTime = element[6].GetInt64(),
            QuoteAssetVolume = decimal.Parse(element[7].GetString()!),
            NumberOfTrades = element[8].GetInt32()
        };
    }
}
