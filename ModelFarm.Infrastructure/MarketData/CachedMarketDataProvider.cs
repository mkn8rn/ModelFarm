using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Infrastructure.Persistence;
using ModelFarm.Infrastructure.Persistence.Entities;

namespace ModelFarm.Infrastructure.MarketData;

/// <summary>
/// Wraps a market data provider with database caching.
/// Checks DB first, fetches missing ranges from API, stores results.
/// </summary>
public sealed class CachedMarketDataProvider : IMarketDataProvider
{
    private readonly IMarketDataProvider _inner;
    private readonly IDbContextFactory<DataDbContext> _dbFactory;

    public CachedMarketDataProvider(IMarketDataProvider inner, IDbContextFactory<DataDbContext> dbFactory)
    {
        _inner = inner;
        _dbFactory = dbFactory;
    }

    public Exchange Exchange => _inner.Exchange;

    public Task<IReadOnlyList<TradingSymbol>> GetSymbolsAsync(CancellationToken cancellationToken = default)
        => _inner.GetSymbolsAsync(cancellationToken);

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

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Find what we already have in the database
        var cached = await db.Klines
            .Where(k => k.Exchange == Exchange 
                     && k.Symbol == symbol 
                     && k.Interval == interval
                     && k.OpenTime >= startMs 
                     && k.OpenTime < endMs)
            .OrderBy(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        var cachedSet = cached.Select(k => k.OpenTime).ToHashSet();
        
        // Find gaps in the data
        var gaps = FindGaps(startMs, endMs, interval, cachedSet);

        // Track total fetched for progress reporting
        var totalFetched = cached.Count;

        // Fetch missing data from API and store using upsert to handle concurrent inserts
        foreach (var (gapStart, gapEnd) in gaps)
        {
            var gapStartUtc = DateTimeOffset.FromUnixTimeMilliseconds(gapStart).UtcDateTime;
            var gapEndUtc = DateTimeOffset.FromUnixTimeMilliseconds(gapEnd).UtcDateTime;

            var batchKlines = new List<KlineEntity>();
            await foreach (var kline in _inner.GetKlinesAsync(symbol, interval, gapStartUtc, gapEndUtc, null, cancellationToken))
            {
                if (!cachedSet.Contains(kline.OpenTime))
                {
                    var entity = KlineEntity.FromKline(Exchange, symbol, interval, kline);
                    batchKlines.Add(entity);
                    cachedSet.Add(kline.OpenTime);
                    totalFetched++;
                    
                    // Report progress during fetch
                    progress?.Report(totalFetched);
                }

                // Batch insert every 1000 records to avoid memory issues
                if (batchKlines.Count >= 1000)
                {
                    await UpsertKlinesAsync(db, batchKlines, cancellationToken);
                    batchKlines.Clear();
                }
            }

            // Insert remaining batch
            if (batchKlines.Count > 0)
            {
                await UpsertKlinesAsync(db, batchKlines, cancellationToken);
            }
        }

        // Return all data (cached + new) in order
        var allKlines = await db.Klines
            .Where(k => k.Exchange == Exchange 
                     && k.Symbol == symbol 
                     && k.Interval == interval
                     && k.OpenTime >= startMs 
                     && k.OpenTime < endMs)
            .OrderBy(k => k.OpenTime)
            .ToListAsync(cancellationToken);

        foreach (var entity in allKlines)
        {
            yield return entity.ToKline();
        }
    }

    /// <summary>
    /// Inserts klines using ON CONFLICT DO NOTHING to handle concurrent inserts gracefully.
    /// </summary>
    private static async Task UpsertKlinesAsync(DataDbContext db, List<KlineEntity> klines, CancellationToken cancellationToken)
    {
        if (klines.Count == 0) return;

        // Use raw SQL for efficient bulk upsert with ON CONFLICT DO NOTHING
        var sql = """
            INSERT INTO data.klines (exchange, symbol, interval, open_time, close_time, open, high, low, close, volume, quote_asset_volume, number_of_trades)
            VALUES {0}
            ON CONFLICT (exchange, symbol, interval, open_time) DO NOTHING
            """;

        var values = string.Join(",\n", klines.Select((k, i) => 
            $"({(int)k.Exchange}, '{k.Symbol}', {(int)k.Interval}, {k.OpenTime}, {k.CloseTime}, {k.Open}, {k.High}, {k.Low}, {k.Close}, {k.Volume}, {k.QuoteAssetVolume}, {k.NumberOfTrades})"));

        await db.Database.ExecuteSqlRawAsync(string.Format(sql, values), cancellationToken);
    }


    private static List<(long Start, long End)> FindGaps(long startMs, long endMs, KlineInterval interval, HashSet<long> existingTimes)
    {
        var gaps = new List<(long, long)>();
        var intervalMs = GetIntervalMs(interval);
        
        long? gapStart = null;
        var current = startMs;

        while (current < endMs)
        {
            if (!existingTimes.Contains(current))
            {
                gapStart ??= current;
            }
            else if (gapStart.HasValue)
            {
                gaps.Add((gapStart.Value, current));
                gapStart = null;
            }
            current += intervalMs;
        }

        if (gapStart.HasValue)
        {
            gaps.Add((gapStart.Value, endMs));
        }

        return gaps;
    }

    private static long GetIntervalMs(KlineInterval interval) => interval switch
    {
        KlineInterval.OneMinute => 60_000,
        KlineInterval.FiveMinutes => 300_000,
        KlineInterval.FifteenMinutes => 900_000,
        KlineInterval.OneHour => 3_600_000,
        KlineInterval.FourHours => 14_400_000,
        KlineInterval.OneDay => 86_400_000,
        _ => 3_600_000
    };
}
