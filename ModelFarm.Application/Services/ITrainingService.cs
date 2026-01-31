using ModelFarm.Contracts.Training;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for managing training configurations and jobs for quant trading ML models.
/// </summary>
public interface ITrainingService
{
    // ==================== Configuration Management ====================

    /// <summary>
    /// Creates a new training configuration.
    /// </summary>
    Task<TrainingConfiguration> CreateConfigurationAsync(CreateTrainingConfigurationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a training configuration by ID.
    /// </summary>
    Task<TrainingConfiguration?> GetConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all training configurations.
    /// </summary>
    Task<IReadOnlyList<TrainingConfiguration>> GetAllConfigurationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a training configuration.
    /// </summary>
    Task<TrainingConfiguration> UpdateConfigurationAsync(Guid configurationId, UpdateTrainingConfigurationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a training configuration.
    /// </summary>
    Task<bool> DeleteConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default);

    // ==================== Training Job Management ====================

    /// <summary>
    /// Starts a new training job.
    /// </summary>
    Task<TrainingJob> StartTrainingAsync(TrainingJobRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a training job.
    /// </summary>
    Task<TrainingJob?> GetTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all training jobs, optionally filtered by status.
    /// </summary>
    Task<IReadOnlyList<TrainingJob>> GetAllTrainingJobsAsync(TrainingJobStatus? statusFilter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running training job.
    /// </summary>
    Task<bool> CancelTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses a running training job.
    /// </summary>
    Task<bool> PauseTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a paused training job (continues in-memory training loop).
    /// </summary>
    Task<bool> ResumePausedJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries a failed or completed training job from the beginning.
    /// </summary>
    Task<bool> RetryTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a training job from its last checkpoint (for interrupted jobs).
    /// </summary>
    Task<bool> ResumeTrainingJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets training jobs for a specific configuration.
    /// </summary>
    Task<IReadOnlyList<TrainingJob>> GetJobsForConfigurationAsync(Guid configurationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to create a new training configuration.
/// </summary>
public sealed record CreateTrainingConfigurationRequest
{
    public required ConfigurationType Type { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required Guid DatasetId { get; init; }
    public required ModelType ModelType { get; init; }

    // Model architecture
    public int MaxLags { get; init; } = 4;
    public int ForecastHorizon { get; init; } = 1;
    public int[] HiddenLayerSizes { get; init; } = [64, 32];
    public double DropoutRate { get; init; } = 0.2;

    // Training hyperparameters
    public double LearningRate { get; init; } = 0.001;
    public int BatchSize { get; init; } = 32;
    public int MaxEpochs { get; init; } = 10000;
    public int EarlyStoppingPatience { get; init; } = 10;
    public double ValidationSplit { get; init; } = 0.2;
    public double TestSplit { get; init; } = 0.1;
    public int RandomSeed { get; init; } = 42;

    // Performance requirements
    public required PerformanceRequirements PerformanceRequirements { get; init; }

    // Trading environment
    public required TradingEnvironmentConfig TradingEnvironment { get; init; }
}

/// <summary>
/// Request to update an existing training configuration.
/// </summary>
public sealed record UpdateTrainingConfigurationRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }

    // Model architecture
    public int? MaxLags { get; init; }
    public int? ForecastHorizon { get; init; }
    public int[]? HiddenLayerSizes { get; init; }
    public double? DropoutRate { get; init; }

    // Training hyperparameters
    public double? LearningRate { get; init; }
    public int? BatchSize { get; init; }
    public int? MaxEpochs { get; init; }
    public int? EarlyStoppingPatience { get; init; }
    public double? ValidationSplit { get; init; }
    public double? TestSplit { get; init; }
    public int? RandomSeed { get; init; }

    // Performance requirements
    public PerformanceRequirements? PerformanceRequirements { get; init; }

    // Trading environment
    public TradingEnvironmentConfig? TradingEnvironment { get; init; }
}
