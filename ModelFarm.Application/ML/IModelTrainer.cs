using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.ML;

/// <summary>
/// Interface for ML model training.
/// </summary>
public interface IModelTrainer
{
    /// <summary>
    /// Trains a model and returns the result.
    /// </summary>
    Task<ModelTrainingResult> TrainAsync(
        TrainingData trainData,
        TrainingData validationData,
        TrainingConfiguration config,
        IProgress<TrainingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Trains a model with checkpoint support for resumable training.
    /// </summary>
    Task<ModelTrainingResult> TrainWithCheckpointsAsync(
        TrainingData trainData,
        TrainingData validationData,
        TrainingConfiguration config,
        CheckpointManager checkpointManager,
        Guid jobId,
        TrainingCheckpoint? resumeFromCheckpoint,
        NormalizationStats normStats,
        IProgress<TrainingProgress>? progress = null,
        Func<int, double, Task>? onCheckpointSaved = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a trained model on test data.
    /// </summary>
    Task<ModelEvaluationResult> EvaluateAsync(
        ITrainedModel model,
        TrainingData testData,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for a trained model that can make predictions.
/// </summary>
public interface ITrainedModel
{
    /// <summary>
    /// Unique identifier for this model.
    /// </summary>
    Guid ModelId { get; }

    /// <summary>
    /// Predicts the target value for the given features.
    /// </summary>
    float Predict(double[] features);

    /// <summary>
    /// Predicts target values for multiple samples.
    /// </summary>
    float[] PredictBatch(IReadOnlyList<double[]> features);

    /// <summary>
    /// Saves the model to a file.
    /// </summary>
    Task SaveAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets model metadata.
    /// </summary>
    ModelMetadata GetMetadata();
}

/// <summary>
/// Result from model training.
/// </summary>
public sealed record ModelTrainingResult
{
    public required ITrainedModel Model { get; init; }
    public required int EpochsTrained { get; init; }
    public required double FinalTrainingLoss { get; init; }
    public required double FinalValidationLoss { get; init; }
    public required double BestValidationLoss { get; init; }
    public required bool EarlyStopTriggered { get; init; }
    public required TimeSpan TrainingDuration { get; init; }
    public required List<EpochMetrics> EpochHistory { get; init; }
}

/// <summary>
/// Metrics for a single training epoch.
/// </summary>
public sealed record EpochMetrics
{
    public required int Epoch { get; init; }
    public required double TrainingLoss { get; init; }
    public required double ValidationLoss { get; init; }
    public required TimeSpan Duration { get; init; }
}

/// <summary>
/// Progress during training.
/// </summary>
public sealed record TrainingProgress
{
    public required int CurrentEpoch { get; init; }
    public required int TotalEpochs { get; init; }
    public required double TrainingLoss { get; init; }
    public required double ValidationLoss { get; init; }
    public required double BestValidationLoss { get; init; }
    public required int EpochsSinceImprovement { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Result from model evaluation.
/// </summary>
public sealed record ModelEvaluationResult
{
    public required double MeanSquaredError { get; init; }
    public required double RootMeanSquaredError { get; init; }
    public required double MeanAbsoluteError { get; init; }
    public required double RSquared { get; init; }
    public required IReadOnlyList<PredictionResult> Predictions { get; init; }
}

/// <summary>
/// A single prediction result.
/// </summary>
public sealed record PredictionResult
{
    public required DateTime Timestamp { get; init; }
    public required float Actual { get; init; }
    public required float Predicted { get; init; }
    public required float ClosePrice { get; init; }
}

/// <summary>
/// Model metadata.
/// </summary>
public sealed record ModelMetadata
{
    public required Guid ModelId { get; init; }
    public required ModelType ModelType { get; init; }
    public required string[] FeatureNames { get; init; }
    public required NormalizationStats? NormalizationStats { get; init; }
    public required DateTime TrainedAtUtc { get; init; }
    public required int EpochsTrained { get; init; }
    public required double BestValidationLoss { get; init; }
}
