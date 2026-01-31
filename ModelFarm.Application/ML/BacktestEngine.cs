using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.ML;

/// <summary>
/// Backtesting engine for evaluating trading strategies based on model predictions.
/// Implements the strategy logic from the quant trading notebook:
/// - Buy when predicted return > 0
/// - Sell when predicted return < 0
/// Memory-optimized: uses downsampled equity curve and bounded trade storage.
/// </summary>
public sealed class BacktestEngine
{
    /// <summary>
    /// Maximum number of equity curve points to store (downsamples if exceeded).
    /// </summary>
    private const int MaxEquityCurvePoints = 1000;

    /// <summary>
    /// Maximum number of trades to store in the result.
    /// </summary>
    private const int MaxStoredTrades = 500;

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
        int predictionCount = predictions.Count;
        if (predictionCount == 0)
            return CreateEmptyResult(config.InitialCapital);

        // Calculate downsampling rate for equity curve
        int sampleEveryN = Math.Max(1, predictionCount / MaxEquityCurvePoints);
        
        List<Trade> trades = new();
        List<EquityPoint> equityCurve = new(Math.Min(predictionCount, MaxEquityCurvePoints + 1));
        
        // Use running statistics for daily returns instead of storing all
        double sumReturns = 0;
        double sumSquaredReturns = 0;
        double sumNegativeSquaredReturns = 0;
        int returnCount = 0;
        int negativeReturnCount = 0;
        decimal totalFees = 0;

        // Use taker fee rate for market orders (most common in algorithmic trading)
        double feeRate = (double)config.Fees.TakerFeeRate;
        double maxPositionSize = (double)config.MaxPositionSizeRatio;

        double equity = (double)config.InitialCapital;
        double position = 0; // Position size in base currency
        double entryPrice = 0;
        DateTime entryTime = default;
        double peakEquity = equity;
        double maxDrawdown = 0;
        double prevEquity = equity;

        equityCurve.Add(new EquityPoint { Timestamp = predictions[0].Timestamp, Equity = equity });

        for (int i = 0; i < predictionCount; i++)
        {
            PredictionResult prediction = predictions[i];
            float predictedReturn = prediction.Predicted;
            double currentPrice = (double)prediction.ClosePrice;

            // Trading signal: buy if predicted return > 0, sell if < 0
            TradeSignal signal = predictedReturn > 0 ? TradeSignal.Buy : TradeSignal.Sell;

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
                    
                    // Only store trade if under limit
                    if (trades.Count < MaxStoredTrades)
                    {
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
                    
                    // Only store trade if under limit
                    if (trades.Count < MaxStoredTrades)
                    {
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

            // Downsample equity curve - only store every Nth point
            if (i % sampleEveryN == 0 || i == predictions.Count - 1)
            {
                equityCurve.Add(new EquityPoint { Timestamp = prediction.Timestamp, Equity = mtmEquity });
            }

            // Track peak and drawdown
            if (mtmEquity > peakEquity)
                peakEquity = mtmEquity;

            var drawdown = (peakEquity - mtmEquity) / peakEquity;
            if (drawdown > maxDrawdown)
                maxDrawdown = drawdown;

            // Running statistics for returns (instead of storing all returns)
            if (i > 0 && prevEquity > 0)
            {
                var periodReturn = (mtmEquity - prevEquity) / prevEquity;
                sumReturns += periodReturn;
                sumSquaredReturns += periodReturn * periodReturn;
                returnCount++;
                
                if (periodReturn < 0)
                {
                    sumNegativeSquaredReturns += periodReturn * periodReturn;
                    negativeReturnCount++;
                }
            }
            prevEquity = mtmEquity;
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

            if (trades.Count < MaxStoredTrades)
            {
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
        }

        // Calculate final metrics using running statistics
        var totalReturn = (equity - (double)config.InitialCapital) / (double)config.InitialCapital;
        var periodCount = predictions.Count;
        var annualizedReturn = Math.Pow(1 + totalReturn, annualizationFactor / periodCount) - 1;

        // Sharpe and Sortino ratios from running statistics
        var (sharpe, sortino) = CalculateRiskAdjustedReturnsFromStats(
            sumReturns, sumSquaredReturns, sumNegativeSquaredReturns, 
            returnCount, negativeReturnCount, annualizationFactor);

        // Win/loss statistics - single pass instead of multiple LINQ queries
        int winningCount = 0;
        int losingCount = 0;
        double grossProfit = 0;
        double grossLoss = 0;
        double sumWinPnl = 0;
        double sumLossPnl = 0;
        
        int tradeCount = trades.Count;
        for (int i = 0; i < tradeCount; i++)
        {
            Trade trade = trades[i];
            if (trade.PnL > 0)
            {
                winningCount++;
                grossProfit += trade.PnL;
                sumWinPnl += trade.PnL;
            }
            else if (trade.PnL < 0)
            {
                losingCount++;
                grossLoss += Math.Abs(trade.PnL);
                sumLossPnl += trade.PnL;
            }
        }

        double winRate = tradeCount > 0 ? (double)winningCount / tradeCount : 0;
        double profitFactor = grossLoss > 0 ? grossProfit / grossLoss : grossProfit > 0 ? double.PositiveInfinity : 0;

        // Calmar ratio
        double calmarRatio = maxDrawdown > 0 ? annualizedReturn / maxDrawdown : 0;

        // Consecutive wins/losses
        (int maxConsecutiveWins, int maxConsecutiveLosses) = CalculateConsecutiveStreaks(trades);

        // Average holding period - single pass
        long totalTicks = 0;
        for (int i = 0; i < tradeCount; i++)
        {
            totalTicks += (trades[i].ExitTime - trades[i].EntryTime).Ticks;
        }
        TimeSpan avgHoldingPeriod = tradeCount > 0 ? TimeSpan.FromTicks(totalTicks / tradeCount) : TimeSpan.Zero;

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
                TotalTrades = tradeCount,
                WinningTrades = winningCount,
                LosingTrades = losingCount,
                AverageWin = winningCount > 0 ? sumWinPnl / winningCount : 0,
                AverageLoss = losingCount > 0 ? sumLossPnl / losingCount : 0,
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

    /// <summary>
    /// Calculates risk-adjusted returns from running statistics (memory efficient).
    /// </summary>
    private static (double sharpe, double sortino) CalculateRiskAdjustedReturnsFromStats(
        double sumReturns,
        double sumSquaredReturns,
        double sumNegativeSquaredReturns,
        int count,
        int negativeCount,
        double annualizationFactor)
    {
        if (count < 2)
            return (0, 0);

        var meanReturn = sumReturns / count;
        var variance = (sumSquaredReturns / count) - (meanReturn * meanReturn);
        var stdDev = Math.Sqrt(Math.Max(0, variance));

        // Downside deviation (for Sortino)
        var downsideDev = negativeCount > 0 
            ? Math.Sqrt(sumNegativeSquaredReturns / negativeCount) 
            : 0;

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
