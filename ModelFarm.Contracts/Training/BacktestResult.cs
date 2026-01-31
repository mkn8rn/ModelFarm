namespace ModelFarm.Contracts.Training;

/// <summary>
/// Complete backtest result.
/// </summary>
public sealed record BacktestResult
{
    public required BacktestMetrics Metrics { get; init; }
    public required IReadOnlyList<Trade> Trades { get; init; }
    public required IReadOnlyList<EquityPoint> EquityCurve { get; init; }
    public required double FinalEquity { get; init; }
}

/// <summary>
/// A single trade execution.
/// </summary>
public sealed record Trade
{
    public required DateTime EntryTime { get; init; }
    public required DateTime ExitTime { get; init; }
    public required TradeDirection Direction { get; init; }
    public required double EntryPrice { get; init; }
    public required double ExitPrice { get; init; }
    public required double PnL { get; init; }
    public required double ReturnPercent { get; init; }
}

/// <summary>
/// Equity curve point.
/// </summary>
public sealed record EquityPoint
{
    public required DateTime Timestamp { get; init; }
    public required double Equity { get; init; }
}

public enum TradeSignal
{
    Hold,
    Buy,
    Sell
}

public enum TradeDirection
{
    Long,
    Short
}
