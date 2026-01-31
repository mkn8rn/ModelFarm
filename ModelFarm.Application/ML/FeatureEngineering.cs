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
    /// Memory-optimized: calculates log returns incrementally without storing full array.
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

        // Pre-calculate expected sample count to avoid list resizing
        var expectedSamples = klines.Count - maxLags - forecastHorizon;
        var samples = new List<TrainingSample>(expectedSamples);

        // Keep a rolling window of log returns to avoid storing the full array
        // We need maxLags + forecastHorizon log returns in the window
        var windowSize = maxLags + forecastHorizon;
        var logReturnWindow = new float[windowSize];
        var windowIndex = 0;
        var filledCount = 0;

        // Process klines in a single pass
        for (int i = 1; i < klines.Count; i++)
        {
            // Calculate current log return
            var logReturn = (float)Math.Log((double)(klines[i].Close / klines[i - 1].Close));
            
            // Store in circular buffer
            logReturnWindow[windowIndex] = logReturn;
            windowIndex = (windowIndex + 1) % windowSize;
            filledCount++;

            // Once we have enough history, start creating samples
            if (filledCount >= windowSize)
            {
                var features = new float[maxLags];
                
                // Extract lag features from circular buffer
                // The most recent log return is at (windowIndex - 1), going backwards
                for (int lag = 0; lag < maxLags; lag++)
                {
                    // lag 0 = 1 period ago, lag 1 = 2 periods ago, etc.
                    var idx = (windowIndex - forecastHorizon - lag - 1 + windowSize) % windowSize;
                    features[lag] = logReturnWindow[idx];
                }

                // Target is the most recent log return (forecastHorizon - 1 positions back from window end)
                var targetIdx = (windowIndex - 1 + windowSize) % windowSize;
                var target = logReturnWindow[targetIdx];

                // The corresponding kline index for this sample
                var klineIdx = i - forecastHorizon + 1;
                
                samples.Add(new TrainingSample
                {
                    Timestamp = klines[klineIdx].OpenTimeUtc,
                    Features = features,
                    Target = target,
                    ClosePrice = (float)klines[klineIdx].Close
                });
            }
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
    /// Memory-optimized: uses ArraySegment-like slicing where possible.
    /// </summary>
    public static (TrainingData train, TrainingData validation, TrainingData test) SplitData(
        TrainingData data,
        double validationSplit = 0.2,
        double testSplit = 0.1)
    {
        // Validate split values
        if (validationSplit < 0 || validationSplit >= 1)
            throw new ArgumentException($"Validation split must be between 0 and 1 (got {validationSplit})", nameof(validationSplit));
        if (testSplit < 0 || testSplit >= 1)
            throw new ArgumentException($"Test split must be between 0 and 1 (got {testSplit})", nameof(testSplit));
        if (validationSplit + testSplit >= 1)
            throw new ArgumentException($"Validation ({validationSplit}) + test ({testSplit}) splits must sum to less than 1");

        var totalSamples = data.Samples.Count;
        
        if (totalSamples < 10)
            throw new InvalidOperationException($"Not enough data samples ({totalSamples}). Need at least 10 samples for train/validation/test split.");

        var testSize = Math.Max(1, (int)(totalSamples * testSplit));
        var validationSize = Math.Max(1, (int)(totalSamples * validationSplit));
        var trainSize = totalSamples - testSize - validationSize;

        if (trainSize < 5)
            throw new InvalidOperationException($"Not enough training samples ({trainSize}). Dataset has {totalSamples} samples but validation ({validationSplit:P0}) and test ({testSplit:P0}) splits leave insufficient data for training.");

        // Use GetRange for more efficient list slicing (avoids LINQ overhead)
        var samples = data.Samples as List<TrainingSample> ?? data.Samples.ToList();
        
        var trainSamples = samples.GetRange(0, trainSize);
        var validationSamples = samples.GetRange(trainSize, validationSize);
        var testSamples = samples.GetRange(trainSize + validationSize, testSize);

        return (
            data with { Samples = trainSamples },
            data with { Samples = validationSamples },
            data with { Samples = testSamples }
        );
    }

    /// <summary>
    /// Normalizes features using z-score normalization.
    /// Returns the statistics needed to normalize new data.
    /// Memory-optimized: computes stats in a single pass using Welford's algorithm.
    /// </summary>
    public static (TrainingData normalizedData, NormalizationStats stats) NormalizeFeatures(TrainingData data)
    {
        if (data.Samples.Count == 0)
            throw new InvalidOperationException("Cannot normalize empty dataset");

        var featureCount = data.FeatureNames.Length;
        var sampleCount = data.Samples.Count;

        // Use Welford's online algorithm for numerically stable mean/variance calculation
        var means = new double[featureCount];
        var m2 = new double[featureCount]; // Sum of squared differences from mean
        var stds = new double[featureCount];

        // Single pass to calculate mean and variance
        for (int n = 0; n < sampleCount; n++)
        {
            var sample = data.Samples[n];
            for (int f = 0; f < featureCount; f++)
            {
                var x = sample.Features[f];
                var delta = x - means[f];
                means[f] += delta / (n + 1);
                m2[f] += delta * (x - means[f]);
            }
        }

        // Calculate standard deviations
        for (int f = 0; f < featureCount; f++)
        {
            stds[f] = Math.Sqrt(m2[f] / sampleCount);
            if (stds[f] < 1e-8) stds[f] = 1.0; // Avoid division by zero
        }

        // Normalize samples - create new feature arrays
        var normalizedSamples = new List<TrainingSample>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            var s = data.Samples[i];
            var normalizedFeatures = new float[featureCount];
            for (int f = 0; f < featureCount; f++)
            {
                normalizedFeatures[f] = (float)((s.Features[f] - means[f]) / stds[f]);
            }
            normalizedSamples.Add(s with { Features = normalizedFeatures });
        }

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
        var featureCount = stats.Means.Length;
        var sampleCount = data.Samples.Count;
        
        var normalizedSamples = new List<TrainingSample>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            var s = data.Samples[i];
            var normalizedFeatures = new float[featureCount];
            for (int f = 0; f < featureCount; f++)
            {
                normalizedFeatures[f] = (float)((s.Features[f] - stats.Means[f]) / stats.StandardDeviations[f]);
            }
            normalizedSamples.Add(s with { Features = normalizedFeatures });
        }

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
/// Uses float[] instead of double[] for 50% memory savings on features.
/// </summary>
public sealed record TrainingSample
{
    public required DateTime Timestamp { get; init; }
    public required float[] Features { get; init; }
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
