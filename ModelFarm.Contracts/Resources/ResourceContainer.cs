namespace ModelFarm.Contracts.Resources;

/// <summary>
/// Type of resource managed by a container.
/// </summary>
public enum ResourceType
{
    /// <summary>
    /// CPU threads for preprocessing, normalization, and backtesting.
    /// </summary>
    CPU,
    
    /// <summary>
    /// GPU devices for training and inference.
    /// </summary>
    GPU,
    
    /// <summary>
    /// RAM memory limit in bytes.
    /// </summary>
    RAM
}

/// <summary>
/// A resource container that limits and tracks resource usage.
/// Jobs acquire resources from containers before executing.
/// </summary>
public sealed record ResourceContainer
{
    /// <summary>
    /// Unique identifier for the container.
    /// </summary>
    public required Guid Id { get; init; }
    
    /// <summary>
    /// Display name for the container (e.g., "Training Pool", "Background Tasks").
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Type of resource this container manages.
    /// </summary>
    public required ResourceType Type { get; init; }
    
    /// <summary>
    /// Maximum capacity (threads for CPU, devices for GPU, bytes for RAM).
    /// </summary>
    public required long MaxCapacity { get; init; }
    
    /// <summary>
    /// Optional description of the container's purpose.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// If true, this is the default container for its resource type.
    /// New configurations use default containers if none specified.
    /// </summary>
    public bool IsDefault { get; init; }
    
    /// <summary>
    /// When the container was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Runtime status of a resource container (not persisted).
/// </summary>
public sealed record ResourceContainerStatus
{
    /// <summary>
    /// The container's ID.
    /// </summary>
    public required Guid ContainerId { get; init; }
    
    /// <summary>
    /// The container's name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Type of resource.
    /// </summary>
    public required ResourceType Type { get; init; }
    
    /// <summary>
    /// Maximum capacity (threads for CPU, devices for GPU, bytes for RAM).
    /// </summary>
    public required long MaxCapacity { get; init; }
    
    /// <summary>
    /// Current usage (threads/devices in use, or bytes allocated for RAM).
    /// </summary>
    public required long CurrentUsage { get; init; }
    
    /// <summary>
    /// Number of jobs waiting for resources.
    /// </summary>
    public required int QueuedJobs { get; init; }
    
    /// <summary>
    /// IDs of jobs currently using this container.
    /// </summary>
    public IReadOnlyList<Guid> ActiveJobIds { get; init; } = [];
    
    /// <summary>
    /// Whether this is the default container for its type.
    /// </summary>
    public bool IsDefault { get; init; }
    
    /// <summary>
    /// Description of the container.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Detected hardware resources on the system.
/// </summary>
public sealed record DetectedHardware
{
    /// <summary>
    /// Number of logical CPU cores detected.
    /// </summary>
    public required int CpuCores { get; init; }
    
    /// <summary>
    /// Number of GPU devices detected.
    /// </summary>
    public required int GpuDevices { get; init; }
    
    /// <summary>
    /// Names of detected GPU devices.
    /// </summary>
    public IReadOnlyList<string> GpuNames { get; init; } = [];
    
    /// <summary>
    /// Whether CUDA is available.
    /// </summary>
    public bool CudaAvailable { get; init; }
    
    /// <summary>
    /// Total system RAM in bytes.
    /// </summary>
    public required long TotalRamBytes { get; init; }
    
    /// <summary>
    /// Available RAM in bytes.
    /// </summary>
    public required long AvailableRamBytes { get; init; }
}
