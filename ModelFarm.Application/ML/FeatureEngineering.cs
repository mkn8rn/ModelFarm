using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.ML;

/// <summary>
/// Feature engineering for quant trading models.
/// Creates lag features and calculates log returns similar to the Python notebook implementation.
/// </summary>
public static class FeatureEngineering
{
    /// <summary>
    /// Creates a feature matrix from OHLC data with lagged log returns.
    /// This mirrors the feature generation in the quant trading notebook.
    /// </summary>
    /// <param name="klines">Raw kline/candlestick data</param>
    /// <param name="maxLags">Number of lagged features to create</param>
    /// <param name="forecastHorizon">How many steps ahead to predict</param>
    /// <returns>Prepared training data with features and targets</returns>
    public static TrainingData PrepareTrainingData(
        IReadOnlyList<Kline> klines,
        int maxLags = 4,
        int forecastHorizon = 1)
    {
        ArgumentNullException.ThrowIfNull(klines);
        if (klines.Count < maxLags + forecastHorizon + 1)
            throw new ArgumentException($"Need at least {maxLags + forecastHorizon + 1} data points");

        // Calculate log returns: ln(close[t] / close[t-1])
        var logReturns = CalculateLogReturns(klines);

        // Create lagged features and target
        var samples = new List<TrainingSample>();

        // Start from index where we have enough history for lags
        // End before where we don't have enough future data for the target
        for (int i = maxLags; i < logReturns.Length - forecastHorizon; i++)
        {
            var features = new double[maxLags];
            for (int lag = 0; lag < maxLags; lag++)
            {
                // Feature at lag k is the log return from k periods ago
                features[lag] = logReturns[i - lag - 1];
            }

            // Target is the future log return (forecast_horizon steps ahead)
            var target = logReturns[i + forecastHorizon - 1];

            samples.Add(new TrainingSample
            {
                Timestamp = klines[i].OpenTimeUtc,
                Features = features,
                Target = (float)target,
                ClosePrice = (float)klines[i].Close
            });
        }

        return new TrainingData
        {
            Samples = samples,
            FeatureNames = Enumerable.Range(1, maxLags).Select(i => $"LogReturn_Lag{i}").ToArray(),
            MaxLags = maxLags,
            ForecastHorizon = forecastHorizon
        };
    }

    /// <summary>
    /// Calculates log returns from close prices.
    /// log_return[t] = ln(close[t] / close[t-1])
    /// </summary>
    public static double[] CalculateLogReturns(IReadOnlyList<Kline> klines)
    {
        if (klines.Count < 2)
            return [];

        var returns = new double[klines.Count - 1];
        for (int i = 1; i < klines.Count; i++)
        {
            returns[i - 1] = Math.Log((double)(klines[i].Close / klines[i - 1].Close));
        }
        return returns;
    }

    /// <summary>
    /// Splits data into train/validation/test sets maintaining temporal order.
    /// </summary>
    public static (TrainingData train, TrainingData validation, TrainingData test) SplitData(
        TrainingData data,
        double validationSplit = 0.2,
        double testSplit = 0.1)
    {
        var totalSamples = data.Samples.Count;
        
        if (totalSamples < 10)
            throw new InvalidOperationException($"Not enough data samples ({totalSamples}). Need at least 10 samples for train/validation/test split.");

        var testSize = Math.Max(1, (int)(totalSamples * testSplit));
        var validationSize = Math.Max(1, (int)(totalSamples * validationSplit));
        var trainSize = totalSamples - testSize - validationSize;

        if (trainSize < 5)
            throw new InvalidOperationException($"Not enough training samples ({trainSize}). Dataset has {totalSamples} samples but validation ({validationSplit:P0}) and test ({testSplit:P0}) splits leave insufficient data for training.");

        var trainSamples = data.Samples.Take(trainSize).ToList();
        var validationSamples = data.Samples.Skip(trainSize).Take(validationSize).ToList();
        var testSamples = data.Samples.Skip(trainSize + validationSize).ToList();

        return (
            data with { Samples = trainSamples },
            data with { Samples = validationSamples },
            data with { Samples = testSamples }
        );
    }

    /// <summary>
    /// Normalizes features using z-score normalization.
    /// Returns the statistics needed to normalize new data.
    /// </summary>
    public static (TrainingData normalizedData, NormalizationStats stats) NormalizeFeatures(TrainingData data)
    {
        if (data.Samples.Count == 0)
            throw new InvalidOperationException("Cannot normalize empty dataset");

        var featureCount = data.FeatureNames.Length;
        var means = new double[featureCount];
        var stds = new double[featureCount];
        var sampleCount = data.Samples.Count;

        // Calculate means
        for (int f = 0; f < featureCount; f++)
        {
            means[f] = data.Samples.Average(s => s.Features[f]);
        }

        // Calculate standard deviations
        for (int f = 0; f < featureCount; f++)
        {
            var sumSquaredDiff = data.Samples.Sum(s => Math.Pow(s.Features[f] - means[f], 2));
            stds[f] = Math.Sqrt(sumSquaredDiff / sampleCount);
            if (stds[f] < 1e-8) stds[f] = 1.0; // Avoid division by zero
        }

        // Normalize samples
        var normalizedSamples = data.Samples.Select(s =>
        {
            var normalizedFeatures = new double[featureCount];
            for (int f = 0; f < featureCount; f++)
            {
                normalizedFeatures[f] = (s.Features[f] - means[f]) / stds[f];
            }
            return s with { Features = normalizedFeatures };
        }).ToList();

        return (
            data with { Samples = normalizedSamples },
            new NormalizationStats { Means = means, StandardDeviations = stds }
        );
    }

    /// <summary>
    /// Applies existing normalization statistics to new data.
    /// </summary>
    public static TrainingData ApplyNormalization(TrainingData data, NormalizationStats stats)
    {
        var normalizedSamples = data.Samples.Select(s =>
        {
            var normalizedFeatures = new double[s.Features.Length];
            for (int f = 0; f < s.Features.Length; f++)
            {
                normalizedFeatures[f] = (s.Features[f] - stats.Means[f]) / stats.StandardDeviations[f];
            }
            return s with { Features = normalizedFeatures };
        }).ToList();

        return data with { Samples = normalizedSamples };
    }
}

/// <summary>
/// Training data container.
/// </summary>
public sealed record TrainingData
{
    public required IReadOnlyList<TrainingSample> Samples { get; init; }
    public required string[] FeatureNames { get; init; }
    public required int MaxLags { get; init; }
    public required int ForecastHorizon { get; init; }
}

/// <summary>
/// A single training sample with features and target.
/// </summary>
public sealed record TrainingSample
{
    public required DateTime Timestamp { get; init; }
    public required double[] Features { get; init; }
    public required float Target { get; init; }
    public required float ClosePrice { get; init; }
}

/// <summary>
/// Statistics for feature normalization.
/// </summary>
public sealed record NormalizationStats
{
    public required double[] Means { get; init; }
    public required double[] StandardDeviations { get; init; }
}
