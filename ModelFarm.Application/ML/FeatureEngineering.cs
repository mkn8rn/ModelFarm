using System.Buffers;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.ML;

/// <summary>
/// Feature engineering for quant trading models.
/// Creates lag features and calculates log returns similar to the Python notebook implementation.
/// Memory-optimized: uses ArrayPool, stackalloc, and avoids LINQ allocations.
/// Server-ready: uses ParallelismCoordinator for global thread pool management.
/// </summary>
public static class FeatureEngineering
{
    /// <summary>
    /// Creates a feature matrix from OHLC data with lagged log returns.
    /// This mirrors the feature generation in the quant trading notebook.
    /// Memory-optimized: calculates log returns incrementally without storing full array.
    /// </summary>
    public static TrainingData PrepareTrainingData(
        IReadOnlyList<Kline> klines,
        int maxLags = 4,
        int forecastHorizon = 1)
    {
        ArgumentNullException.ThrowIfNull(klines);
        if (klines.Count < maxLags + forecastHorizon + 1)
            throw new ArgumentException($"Need at least {maxLags + forecastHorizon + 1} data points");

        // Pre-calculate expected sample count to avoid list resizing
        int expectedSamples = klines.Count - maxLags - forecastHorizon;
        List<TrainingSample> samples = new(expectedSamples);

        // Keep a rolling window of log returns to avoid storing the full array
        int windowSize = maxLags + forecastHorizon;
        
        // Use stackalloc for small windows, otherwise rent from pool
        float[]? rentedArray = null;
        Span<float> logReturnWindow = windowSize <= 64 
            ? stackalloc float[windowSize] 
            : (rentedArray = ArrayPool<float>.Shared.Rent(windowSize)).AsSpan(0, windowSize);
        
        try
        {
            int windowIndex = 0;
            int filledCount = 0;

            // Process klines in a single pass
            int klineCount = klines.Count;
            for (int i = 1; i < klineCount; i++)
            {
                // Calculate current log return
                float logReturn = (float)Math.Log((double)(klines[i].Close / klines[i - 1].Close));
                
                // Store in circular buffer
                logReturnWindow[windowIndex] = logReturn;
                windowIndex = (windowIndex + 1) % windowSize;
                filledCount++;

                // Once we have enough history, start creating samples
                if (filledCount >= windowSize)
                {
                    float[] features = new float[maxLags];
                    
                    // Extract lag features from circular buffer
                    for (int lag = 0; lag < maxLags; lag++)
                    {
                        int idx = (windowIndex - forecastHorizon - lag - 1 + windowSize) % windowSize;
                        features[lag] = logReturnWindow[idx];
                    }

                    // Target is the most recent log return
                    int targetIdx = (windowIndex - 1 + windowSize) % windowSize;
                    float target = logReturnWindow[targetIdx];

                    // The corresponding kline index for this sample
                    int klineIdx = i - forecastHorizon + 1;
                    
                    samples.Add(new TrainingSample
                    {
                        Timestamp = klines[klineIdx].OpenTimeUtc,
                        Features = features,
                        Target = target,
                        ClosePrice = (float)klines[klineIdx].Close
                    });
                }
            }
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<float>.Shared.Return(rentedArray);
            }
        }

        // Build feature names without LINQ
        string[] featureNames = new string[maxLags];
        for (int i = 0; i < maxLags; i++)
        {
            featureNames[i] = $"LogReturn_Lag{i + 1}";
        }

        return new TrainingData
        {
            Samples = samples,
            FeatureNames = featureNames,
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
    /// Memory-optimized: uses List.GetRange for efficient slicing.
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

        int totalSamples = data.Samples.Count;
        
        if (totalSamples < 10)
            throw new InvalidOperationException($"Not enough data samples ({totalSamples}). Need at least 10 samples for train/validation/test split.");

        int testSize = Math.Max(1, (int)(totalSamples * testSplit));
        int validationSize = Math.Max(1, (int)(totalSamples * validationSplit));
        int trainSize = totalSamples - testSize - validationSize;

        if (trainSize < 5)
            throw new InvalidOperationException($"Not enough training samples ({trainSize}). Dataset has {totalSamples} samples but validation ({validationSplit:P0}) and test ({testSplit:P0}) splits leave insufficient data for training.");

        // Use GetRange for efficient list slicing (avoids LINQ overhead)
        List<TrainingSample> samples = data.Samples as List<TrainingSample> ?? new List<TrainingSample>(data.Samples);
        
        List<TrainingSample> trainSamples = samples.GetRange(0, trainSize);
        List<TrainingSample> validationSamples = samples.GetRange(trainSize, validationSize);
        List<TrainingSample> testSamples = samples.GetRange(trainSize + validationSize, testSize);

        return (
            data with { Samples = trainSamples },
            data with { Samples = validationSamples },
            data with { Samples = testSamples }
        );
    }

    /// <summary>
    /// Normalizes features using z-score normalization.
    /// Returns the statistics needed to normalize new data.
    /// Server-ready: uses ParallelismCoordinator for global thread management.
    /// </summary>
    public static (TrainingData normalizedData, NormalizationStats stats) NormalizeFeatures(TrainingData data)
    {
        if (data.Samples.Count == 0)
            throw new InvalidOperationException("Cannot normalize empty dataset");

        int featureCount = data.FeatureNames.Length;
        int sampleCount = data.Samples.Count;
        IReadOnlyList<TrainingSample> samples = data.Samples;

        // Calculate mean and variance
        double[] means = new double[featureCount];
        double[] stds = new double[featureCount];

        // First pass: calculate means (parallel if system allows, otherwise sequential)
        ParallelismCoordinator.For(0, featureCount, f =>
        {
            double sum = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                sum += samples[i].Features[f];
            }
            means[f] = sum / sampleCount;
        });

        // Second pass: calculate standard deviations
        ParallelismCoordinator.For(0, featureCount, f =>
        {
            double sumSquaredDiff = 0;
            double mean = means[f];
            for (int i = 0; i < sampleCount; i++)
            {
                double diff = samples[i].Features[f] - mean;
                sumSquaredDiff += diff * diff;
            }
            stds[f] = Math.Sqrt(sumSquaredDiff / sampleCount);
            if (stds[f] < 1e-8) stds[f] = 1.0;
        });

        // Normalize samples
        TrainingSample[] normalizedSamples = new TrainingSample[sampleCount];
        ParallelismCoordinator.For(0, sampleCount, i =>
        {
            TrainingSample s = samples[i];
            float[] normalizedFeatures = new float[featureCount];
            for (int f = 0; f < featureCount; f++)
            {
                normalizedFeatures[f] = (float)((s.Features[f] - means[f]) / stds[f]);
            }
            normalizedSamples[i] = s with { Features = normalizedFeatures };
        });

        return (
            data with { Samples = normalizedSamples.ToList() },
            new NormalizationStats { Means = means, StandardDeviations = stds }
        );
    }

    /// <summary>
    /// Applies existing normalization statistics to new data.
    /// Server-ready: uses ParallelismCoordinator for global thread management.
    /// </summary>
    public static TrainingData ApplyNormalization(TrainingData data, NormalizationStats stats)
    {
        int featureCount = stats.Means.Length;
        int sampleCount = data.Samples.Count;
        IReadOnlyList<TrainingSample> samples = data.Samples;
        double[] means = stats.Means;
        double[] stds = stats.StandardDeviations;

        TrainingSample[] normalizedSamples = new TrainingSample[sampleCount];
        ParallelismCoordinator.For(0, sampleCount, i =>
        {
            TrainingSample s = samples[i];
            float[] normalizedFeatures = new float[featureCount];
            for (int f = 0; f < featureCount; f++)
            {
                normalizedFeatures[f] = (float)((s.Features[f] - means[f]) / stds[f]);
            }
            normalizedSamples[i] = s with { Features = normalizedFeatures };
        });

        return data with { Samples = normalizedSamples.ToList() };
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
