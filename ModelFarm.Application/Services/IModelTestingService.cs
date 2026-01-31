using ModelFarm.Application.ML;
using ModelFarm.Contracts.Testing;
using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for testing trained models on datasets.
/// </summary>
public interface IModelTestingService
{
    /// <summary>
    /// Loads a trained model from a completed job's checkpoint.
    /// </summary>
    Task<LoadedModel?> LoadModelAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new test and runs it, returning the test with results.
    /// </summary>
    Task<ModelTest> CreateAndRunTestAsync(
        Guid modelJobId,
        Guid datasetId,
        string? testName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all saved model tests.
    /// </summary>
    Task<IReadOnlyList<ModelTest>> GetAllTestsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific test by ID.
    /// </summary>
    Task<ModelTest?> GetTestAsync(Guid testId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a test.
    /// </summary>
    Task<bool> DeleteTestAsync(Guid testId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available models (completed training jobs with checkpoints).
    /// </summary>
    Task<IReadOnlyList<AvailableModel>> GetAvailableModelsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A loaded model ready for testing.
/// </summary>
public sealed class LoadedModel : IDisposable
{
    public required Guid JobId { get; init; }
    public required string JobName { get; init; }
    public required Guid ConfigurationId { get; init; }
    public required ModelType ModelType { get; init; }
    public required int MaxLags { get; init; }
    public required string[] FeatureNames { get; init; }
    public required NormalizationStats NormalizationStats { get; init; }
    public required TradingEnvironmentConfig TradingConfig { get; init; }
    public required ITrainedModel Model { get; init; }

    public void Dispose() => Model.Dispose();
}

/// <summary>
/// Information about an available model for testing.
/// </summary>
public sealed record AvailableModel
{
    public required Guid JobId { get; init; }
    public required string JobName { get; init; }
    public required string ConfigurationName { get; init; }
    public required ModelType ModelType { get; init; }
    public required Guid DatasetId { get; init; }
    public required string DatasetName { get; init; }
    public required string Symbol { get; init; }
    public required DateTime CompletedAtUtc { get; init; }
    public required double? SharpeRatio { get; init; }
    public required bool MeetsRequirements { get; init; }
}
