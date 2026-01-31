using ModelFarm.Contracts.Training;

namespace ModelFarm.Contracts.Testing;

/// <summary>
/// Status of a model test.
/// </summary>
public enum ModelTestStatus
{
    Running,
    Completed,
    Failed
}

/// <summary>
/// A persisted model test.
/// </summary>
public sealed record ModelTest
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required Guid ModelJobId { get; init; }
    public required Guid DatasetId { get; init; }
    public required string ModelName { get; init; }
    public required string ModelType { get; init; }
    public required string DatasetName { get; init; }
    public required string Symbol { get; init; }
    public required ModelTestStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public ModelTestResult? Result { get; init; }
}

/// <summary>
/// Result from running a model test.
/// </summary>
public sealed record ModelTestResult
{
    public required int TotalPredictions { get; init; }
    public required double MeanSquaredError { get; init; }
    public required double MeanAbsoluteError { get; init; }
    public required double DirectionalAccuracy { get; init; }
    public required BacktestResult BacktestResult { get; init; }
    public required IReadOnlyList<TestPrediction> Predictions { get; init; }
}

/// <summary>
/// A single prediction from a model test.
/// </summary>
public sealed record TestPrediction
{
    public required DateTime Timestamp { get; init; }
    public required decimal ClosePrice { get; init; }
    public required float ActualReturn { get; init; }
    public required float PredictedReturn { get; init; }
    public required string Signal { get; init; }
}
