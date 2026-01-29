using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.MarketData;

namespace ModelFarm.Infrastructure.Persistence.Entities;

[Index(nameof(Exchange), nameof(Symbol), nameof(Interval), nameof(OpenTime), IsUnique = true)]
[Index(nameof(Exchange), nameof(Symbol), nameof(Interval), nameof(OpenTime), nameof(CloseTime))]
public sealed class KlineEntity
{
    public long Id { get; set; }
    
    public required Exchange Exchange { get; set; }
    public required string Symbol { get; set; }
    public required KlineInterval Interval { get; set; }
    
    public required long OpenTime { get; set; }
    public required long CloseTime { get; set; }
    
    public required decimal Open { get; set; }
    public required decimal High { get; set; }
    public required decimal Low { get; set; }
    public required decimal Close { get; set; }
    public required decimal Volume { get; set; }
    public required decimal QuoteAssetVolume { get; set; }
    public required int NumberOfTrades { get; set; }

    public Kline ToKline() => new()
    {
        OpenTime = OpenTime,
        CloseTime = CloseTime,
        Open = Open,
        High = High,
        Low = Low,
        Close = Close,
        Volume = Volume,
        QuoteAssetVolume = QuoteAssetVolume,
        NumberOfTrades = NumberOfTrades
    };

    public static KlineEntity FromKline(Exchange exchange, string symbol, KlineInterval interval, Kline kline) => new()
    {
        Exchange = exchange,
        Symbol = symbol,
        Interval = interval,
        OpenTime = kline.OpenTime,
        CloseTime = kline.CloseTime,
        Open = kline.Open,
        High = kline.High,
        Low = kline.Low,
        Close = kline.Close,
        Volume = kline.Volume,
        QuoteAssetVolume = kline.QuoteAssetVolume,
        NumberOfTrades = kline.NumberOfTrades
    };
}
