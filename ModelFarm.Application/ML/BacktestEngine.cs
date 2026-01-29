using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.ML;

/// <summary>
/// Backtesting engine for evaluating trading strategies based on model predictions.
/// Implements the strategy logic from the quant trading notebook:
/// - Buy when predicted return > 0
/// - Sell when predicted return < 0
/// </summary>
public sealed class BacktestEngine
{
    /// <summary>
    /// Runs a backtest using model predictions.
    /// </summary>
    /// <param name="predictions">Model predictions with actual values</param>
    /// <param name="config">Trading environment configuration</param>
    /// <param name="annualizationFactor">Factor to annualize returns (e.g., sqrt(365*24) for hourly data)</param>
    /// <returns>Backtest performance metrics</returns>
    public BacktestResult RunBacktest(
        IReadOnlyList<PredictionResult> predictions,
        TradingEnvironmentConfig config,
        double annualizationFactor)
    {
        if (predictions.Count == 0)
            return CreateEmptyResult(config.InitialCapital);

        var trades = new List<Trade>();
        var equityCurve = new List<EquityPoint>();
        var dailyReturns = new List<double>();
        decimal totalFees = 0;

        // Use taker fee rate for market orders (most common in algorithmic trading)
        var feeRate = (double)config.Fees.TakerFeeRate;
        var maxPositionSize = (double)config.MaxPositionSizeRatio;

        double equity = (double)config.InitialCapital;
        double position = 0; // Position size in base currency
        double entryPrice = 0;
        DateTime entryTime = default;
        double peakEquity = equity;
        double maxDrawdown = 0;

        equityCurve.Add(new EquityPoint { Timestamp = predictions[0].Timestamp, Equity = equity });

        for (int i = 0; i < predictions.Count; i++)
        {
            var prediction = predictions[i];
            var predictedReturn = prediction.Predicted;
            var currentPrice = (double)prediction.ClosePrice;

            // Trading signal: buy if predicted return > 0, sell if < 0
            var signal = predictedReturn > 0 ? TradeSignal.Buy : TradeSignal.Sell;

            // Execute trades based on signal
            if (signal == TradeSignal.Buy && position <= 0)
            {
                // Close short position if any
                if (position < 0 && config.AllowShortSelling)
                {
                    var fee = Math.Abs(position * currentPrice * feeRate);
                    var pnl = -position * (currentPrice - entryPrice) - fee;
                    equity += pnl;
                    totalFees += (decimal)fee;
                    trades.Add(new Trade
                    {
                        EntryTime = entryTime,
                        ExitTime = prediction.Timestamp,
                        Direction = TradeDirection.Short,
                        EntryPrice = entryPrice,
                        ExitPrice = currentPrice,
                        PnL = pnl,
                        ReturnPercent = pnl / (Math.Abs(position) * entryPrice)
                    });
                }

                // Open long position
                position = (equity * maxPositionSize) / currentPrice;
                entryPrice = currentPrice;
                entryTime = prediction.Timestamp;
                var openFee = position * currentPrice * feeRate;
                equity -= openFee;
                totalFees += (decimal)openFee;
            }
            else if (signal == TradeSignal.Sell && position >= 0)
            {
                // Close long position if any
                if (position > 0)
                {
                    var fee = position * currentPrice * feeRate;
                    var pnl = position * (currentPrice - entryPrice) - fee;
                    equity += pnl;
                    totalFees += (decimal)fee;
                    trades.Add(new Trade
                    {
                        EntryTime = entryTime,
                        ExitTime = prediction.Timestamp,
                        Direction = TradeDirection.Long,
                        EntryPrice = entryPrice,
                        ExitPrice = currentPrice,
                        PnL = pnl,
                        ReturnPercent = pnl / (position * entryPrice)
                    });
                }

                // Open short position only if allowed
                if (config.AllowShortSelling)
                {
                    position = -(equity * maxPositionSize) / currentPrice;
                    entryPrice = currentPrice;
                    entryTime = prediction.Timestamp;
                    var openFee = Math.Abs(position) * currentPrice * feeRate;
                    equity -= openFee;
                    totalFees += (decimal)openFee;
                }
                else
                {
                    position = 0;
                }
            }

            // Mark-to-market equity
            var mtmEquity = equity;
            if (position > 0)
                mtmEquity += position * (currentPrice - entryPrice);
            else if (position < 0)
                mtmEquity -= position * (currentPrice - entryPrice);

            equityCurve.Add(new EquityPoint { Timestamp = prediction.Timestamp, Equity = mtmEquity });

            // Track peak and drawdown
            if (mtmEquity > peakEquity)
                peakEquity = mtmEquity;

            var drawdown = (peakEquity - mtmEquity) / peakEquity;
            if (drawdown > maxDrawdown)
                maxDrawdown = drawdown;

            // Daily returns (approximation - treating each bar as a period)
            if (i > 0)
            {
                var prevEquity = equityCurve[^2].Equity;
                if (prevEquity > 0)
                    dailyReturns.Add((mtmEquity - prevEquity) / prevEquity);
            }
        }

        // Close any remaining position
        if (position != 0)
        {
            var finalPrice = (double)predictions[^1].ClosePrice;
            var fee = Math.Abs(position) * finalPrice * feeRate;
            var finalPnl = position > 0
                ? position * (finalPrice - entryPrice) - fee
                : -position * (finalPrice - entryPrice) - fee;
            equity += finalPnl;
            totalFees += (decimal)fee;

            trades.Add(new Trade
            {
                EntryTime = entryTime,
                ExitTime = predictions[^1].Timestamp,
                Direction = position > 0 ? TradeDirection.Long : TradeDirection.Short,
                EntryPrice = entryPrice,
                ExitPrice = finalPrice,
                PnL = finalPnl,
                ReturnPercent = finalPnl / (Math.Abs(position) * entryPrice)
            });
        }

        // Calculate final metrics
        var totalReturn = (equity - (double)config.InitialCapital) / (double)config.InitialCapital;
        var periodCount = predictions.Count;
        var annualizedReturn = Math.Pow(1 + totalReturn, annualizationFactor / periodCount) - 1;

        // Sharpe and Sortino ratios
        var (sharpe, sortino) = CalculateRiskAdjustedReturns(dailyReturns, annualizationFactor);

        // Win/loss statistics
        var winningTrades = trades.Where(t => t.PnL > 0).ToList();
        var losingTrades = trades.Where(t => t.PnL < 0).ToList();
        var winRate = trades.Count > 0 ? (double)winningTrades.Count / trades.Count : 0;

        // Profit factor
        var grossProfit = winningTrades.Sum(t => t.PnL);
        var grossLoss = Math.Abs(losingTrades.Sum(t => t.PnL));
        var profitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? double.PositiveInfinity : 0;

        // Calmar ratio
        var calmarRatio = maxDrawdown > 0 ? annualizedReturn / maxDrawdown : 0;

        // Consecutive wins/losses
        var (maxConsecutiveWins, maxConsecutiveLosses) = CalculateConsecutiveStreaks(trades);

        // Average holding period
        var avgHoldingPeriod = trades.Count > 0
            ? TimeSpan.FromTicks((long)trades.Average(t => (t.ExitTime - t.EntryTime).Ticks))
            : TimeSpan.Zero;

        return new BacktestResult
        {
            Metrics = new BacktestMetrics
            {
                TotalReturn = totalReturn,
                AnnualizedReturn = annualizedReturn,
                SharpeRatio = sharpe,
                SortinoRatio = sortino,
                MaxDrawdown = maxDrawdown,
                CalmarRatio = calmarRatio,
                WinRate = winRate,
                ProfitFactor = profitFactor,
                TotalTrades = trades.Count,
                WinningTrades = winningTrades.Count,
                LosingTrades = losingTrades.Count,
                AverageWin = winningTrades.Count > 0 ? winningTrades.Average(t => t.PnL) : 0,
                AverageLoss = losingTrades.Count > 0 ? losingTrades.Average(t => t.PnL) : 0,
                MaxConsecutiveWins = maxConsecutiveWins,
                MaxConsecutiveLosses = maxConsecutiveLosses,
                AverageHoldingPeriod = avgHoldingPeriod,
                TotalFeesPaid = totalFees,
                TotalSlippageCost = 0, // Slippage not simulated
                FinalPortfolioValue = (decimal)equity,
                BacktestStartUtc = predictions[0].Timestamp,
                BacktestEndUtc = predictions[^1].Timestamp
            },
            Trades = trades,
            EquityCurve = equityCurve,
            FinalEquity = equity
        };
    }

    private static (double sharpe, double sortino) CalculateRiskAdjustedReturns(
        List<double> returns,
        double annualizationFactor)
    {
        if (returns.Count < 2)
            return (0, 0);

        var meanReturn = returns.Average();
        var variance = returns.Sum(r => Math.Pow(r - meanReturn, 2)) / (returns.Count - 1);
        var stdDev = Math.Sqrt(variance);

        // Downside deviation (for Sortino)
        var downsideReturns = returns.Where(r => r < 0).ToList();
        var downsideVariance = downsideReturns.Count > 0
            ? downsideReturns.Sum(r => Math.Pow(r, 2)) / downsideReturns.Count
            : 0;
        var downsideDev = Math.Sqrt(downsideVariance);

        // Annualize
        var annualizedMean = meanReturn * annualizationFactor;
        var annualizedStdDev = stdDev * Math.Sqrt(annualizationFactor);
        var annualizedDownsideDev = downsideDev * Math.Sqrt(annualizationFactor);

        var sharpe = annualizedStdDev > 0 ? annualizedMean / annualizedStdDev : 0;
        var sortino = annualizedDownsideDev > 0 ? annualizedMean / annualizedDownsideDev : 0;

        return (sharpe, sortino);
    }

    private static (int maxWins, int maxLosses) CalculateConsecutiveStreaks(List<Trade> trades)
    {
        int maxWins = 0, maxLosses = 0;
        int currentWins = 0, currentLosses = 0;

        foreach (var trade in trades)
        {
            if (trade.PnL > 0)
            {
                currentWins++;
                currentLosses = 0;
                maxWins = Math.Max(maxWins, currentWins);
            }
            else if (trade.PnL < 0)
            {
                currentLosses++;
                currentWins = 0;
                maxLosses = Math.Max(maxLosses, currentLosses);
            }
        }

        return (maxWins, maxLosses);
    }

    private static BacktestResult CreateEmptyResult(decimal initialCapital)
    {
        var now = DateTime.UtcNow;
        return new BacktestResult
        {
            Metrics = new BacktestMetrics
            {
                TotalReturn = 0,
                AnnualizedReturn = 0,
                SharpeRatio = 0,
                SortinoRatio = 0,
                MaxDrawdown = 0,
                CalmarRatio = 0,
                WinRate = 0,
                ProfitFactor = 0,
                TotalTrades = 0,
                WinningTrades = 0,
                LosingTrades = 0,
                AverageWin = 0,
                AverageLoss = 0,
                MaxConsecutiveWins = 0,
                MaxConsecutiveLosses = 0,
                AverageHoldingPeriod = TimeSpan.Zero,
                TotalFeesPaid = 0,
                TotalSlippageCost = 0,
                FinalPortfolioValue = initialCapital,
                BacktestStartUtc = now,
                BacktestEndUtc = now
            },
            Trades = [],
            EquityCurve = [],
            FinalEquity = (double)initialCapital
        };
    }
}

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
