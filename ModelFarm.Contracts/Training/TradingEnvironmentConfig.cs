namespace ModelFarm.Contracts.Training;

/// <summary>
/// Fee structure for trading simulation.
/// </summary>
public sealed record TradingFees
{
    /// <summary>
    /// Maker fee as a decimal (e.g., 0.001 for 0.1%).
    /// Maker orders add liquidity to the order book.
    /// </summary>
    public required decimal MakerFeeRate { get; init; }

    /// <summary>
    /// Taker fee as a decimal (e.g., 0.001 for 0.1%).
    /// Taker orders remove liquidity from the order book.
    /// </summary>
    public required decimal TakerFeeRate { get; init; }

    /// <summary>
    /// Funding rate for perpetual futures (per period, e.g., 8h).
    /// Set to 0 for spot trading.
    /// </summary>
    public decimal FundingRate { get; init; } = 0m;

    /// <summary>
    /// Funding rate period in hours (typically 8 for perpetual futures).
    /// </summary>
    public int FundingPeriodHours { get; init; } = 8;

    /// <summary>
    /// Common fee presets.
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// Binance spot VIP 0 tier fees.
        /// </summary>
        public static TradingFees BinanceSpotVip0 => new()
        {
            MakerFeeRate = 0.001m,  // 0.1%
            TakerFeeRate = 0.001m   // 0.1%
        };

        /// <summary>
        /// Binance spot with BNB discount (25% off).
        /// </summary>
        public static TradingFees BinanceSpotWithBnb => new()
        {
            MakerFeeRate = 0.00075m,  // 0.075%
            TakerFeeRate = 0.00075m   // 0.075%
        };

        /// <summary>
        /// Binance USD?-M Futures VIP 0.
        /// </summary>
        public static TradingFees BinanceFuturesVip0 => new()
        {
            MakerFeeRate = 0.0002m,   // 0.02%
            TakerFeeRate = 0.0005m,   // 0.05%
            FundingRate = 0.0001m,    // 0.01% (typical)
            FundingPeriodHours = 8
        };

        /// <summary>
        /// Zero fees for backtesting without fee impact.
        /// </summary>
        public static TradingFees ZeroFees => new()
        {
            MakerFeeRate = 0m,
            TakerFeeRate = 0m
        };
    }
}

/// <summary>
/// Slippage model configuration.
/// </summary>
public sealed record SlippageConfig
{
    /// <summary>
    /// Fixed slippage as a decimal (e.g., 0.0001 for 0.01%).
    /// Applied to every trade.
    /// </summary>
    public decimal FixedSlippageRate { get; init; } = 0m;

    /// <summary>
    /// Volume-based slippage factor.
    /// Slippage = VolumeFactor * (OrderSize / AverageVolume).
    /// </summary>
    public decimal VolumeImpactFactor { get; init; } = 0m;

    /// <summary>
    /// Maximum slippage cap as a decimal.
    /// </summary>
    public decimal MaxSlippageRate { get; init; } = 0.01m; // 1% max

    /// <summary>
    /// Common slippage presets.
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// No slippage (optimistic simulation).
        /// </summary>
        public static SlippageConfig None => new();

        /// <summary>
        /// Conservative fixed slippage.
        /// </summary>
        public static SlippageConfig Conservative => new()
        {
            FixedSlippageRate = 0.0005m,  // 0.05%
            MaxSlippageRate = 0.01m
        };

        /// <summary>
        /// Realistic slippage with volume impact.
        /// </summary>
        public static SlippageConfig Realistic => new()
        {
            FixedSlippageRate = 0.0001m,   // 0.01%
            VolumeImpactFactor = 0.1m,
            MaxSlippageRate = 0.02m
        };
    }
}

/// <summary>
/// Complete trading environment simulation parameters.
/// </summary>
public sealed record TradingEnvironmentConfig
{
    /// <summary>
    /// Initial capital for the simulation.
    /// </summary>
    public required decimal InitialCapital { get; init; }

    /// <summary>
    /// Currency of the initial capital (e.g., "USDT", "USD").
    /// </summary>
    public required string BaseCurrency { get; init; }

    /// <summary>
    /// Fee structure for the simulation.
    /// </summary>
    public required TradingFees Fees { get; init; }

    /// <summary>
    /// Slippage model configuration.
    /// </summary>
    public required SlippageConfig Slippage { get; init; }

    /// <summary>
    /// Maximum leverage allowed (1.0 for spot trading, >1 for margin/futures).
    /// </summary>
    public decimal MaxLeverage { get; init; } = 1m;

    /// <summary>
    /// Whether short selling is allowed.
    /// </summary>
    public bool AllowShortSelling { get; init; } = false;

    /// <summary>
    /// Margin call level as a ratio (e.g., 0.8 means margin call at 80% maintenance).
    /// Only applicable when MaxLeverage > 1.
    /// </summary>
    public decimal MarginCallLevel { get; init; } = 0.8m;

    /// <summary>
    /// Liquidation level as a ratio.
    /// Only applicable when MaxLeverage > 1.
    /// </summary>
    public decimal LiquidationLevel { get; init; } = 0.5m;

    /// <summary>
    /// Maximum position size as a fraction of portfolio (e.g., 1.0 for 100%).
    /// </summary>
    public decimal MaxPositionSizeRatio { get; init; } = 1m;

    /// <summary>
    /// Minimum trade size in base currency.
    /// </summary>
    public decimal MinTradeSize { get; init; } = 10m;

    /// <summary>
    /// Whether to reinvest profits (compound returns).
    /// </summary>
    public bool ReinvestProfits { get; init; } = true;

    /// <summary>
    /// Common environment presets.
    /// </summary>
    public static class Presets
    {
        /// <summary>
        /// Simple spot trading environment.
        /// </summary>
        public static TradingEnvironmentConfig SpotTrading(decimal initialCapital = 10000m) => new()
        {
            InitialCapital = initialCapital,
            BaseCurrency = "USDT",
            Fees = TradingFees.Presets.BinanceSpotVip0,
            Slippage = SlippageConfig.Presets.Conservative,
            MaxLeverage = 1m,
            AllowShortSelling = false
        };

        /// <summary>
        /// Futures trading with moderate leverage.
        /// </summary>
        public static TradingEnvironmentConfig FuturesTrading(decimal initialCapital = 10000m) => new()
        {
            InitialCapital = initialCapital,
            BaseCurrency = "USDT",
            Fees = TradingFees.Presets.BinanceFuturesVip0,
            Slippage = SlippageConfig.Presets.Realistic,
            MaxLeverage = 10m,
            AllowShortSelling = true,
            MarginCallLevel = 0.8m,
            LiquidationLevel = 0.5m
        };

        /// <summary>
        /// Ideal environment for testing strategies without market friction.
        /// </summary>
        public static TradingEnvironmentConfig Ideal(decimal initialCapital = 10000m) => new()
        {
            InitialCapital = initialCapital,
            BaseCurrency = "USDT",
            Fees = TradingFees.Presets.ZeroFees,
            Slippage = SlippageConfig.Presets.None,
            MaxLeverage = 1m,
            AllowShortSelling = false
        };
    }
}
