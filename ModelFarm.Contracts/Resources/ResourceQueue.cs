namespace ModelFarm.Contracts.Resources;

/// <summary>
/// A resource queue that pairs CPU, GPU, and RAM containers together.
/// Jobs are submitted to queues rather than individual containers.
/// </summary>
public sealed record ResourceQueue
{
    /// <summary>
    /// Unique identifier for the queue.
    /// </summary>
    public required Guid Id { get; init; }
    
    /// <summary>
    /// Display name for the queue (e.g., "High Priority", "Background Tasks").
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// Optional description of the queue's purpose.
    /// </summary>
    public string? Description { get; init; }
    
    /// <summary>
    /// The CPU container paired with this queue.
    /// </summary>
    public required Guid CpuContainerId { get; init; }
    
    /// <summary>
    /// The GPU container paired with this queue.
    /// </summary>
    public required Guid GpuContainerId { get; init; }
    
    /// <summary>
    /// The RAM container paired with this queue (optional).
    /// If null, no memory limits are enforced.
    /// </summary>
    public Guid? RamContainerId { get; init; }
    
    /// <summary>
    /// Maximum concurrent jobs that can run in this queue.
    /// </summary>
    public required int MaxConcurrentJobs { get; init; }
    
    /// <summary>
    /// Maximum time a job can run before being cancelled (null = no limit).
    /// </summary>
    public TimeSpan? MaxJobDuration { get; init; }
    
    /// <summary>
    /// Maximum time a job can wait in queue before being cancelled (null = no limit).
    /// </summary>
    public TimeSpan? MaxQueueWaitTime { get; init; }
    
    /// <summary>
    /// If true, this is the default queue for new jobs.
    /// </summary>
    public bool IsDefault { get; init; }
    
    /// <summary>
    /// When the queue was created.
    /// </summary>
    public required DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Runtime status of a resource queue (not persisted).
/// </summary>
public sealed record ResourceQueueStatus
{
    /// <summary>
    /// The queue's ID.
    /// </summary>
    public required Guid QueueId { get; init; }
    
    /// <summary>
    /// The queue's name.
    /// </summary>
    public required string Name { get; init; }
    
    /// <summary>
    /// CPU container ID.
    /// </summary>
    public required Guid CpuContainerId { get; init; }
    
    /// <summary>
    /// CPU container name.
    /// </summary>
    public required string CpuContainerName { get; init; }
    
    /// <summary>
    /// CPU container capacity.
    /// </summary>
    public required int CpuContainerCapacity { get; init; }
    
    /// <summary>
    /// GPU container ID.
    /// </summary>
    public required Guid GpuContainerId { get; init; }
    
    /// <summary>
    /// GPU container name.
    /// </summary>
    public required string GpuContainerName { get; init; }
    
    /// <summary>
    /// GPU container capacity.
    /// </summary>
    public required int GpuContainerCapacity { get; init; }
    
    /// <summary>
    /// RAM container ID (null if no RAM limit).
    /// </summary>
    public Guid? RamContainerId { get; init; }
    
    /// <summary>
    /// RAM container name (null if no RAM limit).
    /// </summary>
    public string? RamContainerName { get; init; }
    
    /// <summary>
    /// RAM container capacity in bytes (null if no RAM limit).
    /// </summary>
    public long? RamContainerCapacity { get; init; }
    
    /// <summary>
    /// Maximum concurrent jobs.
    /// </summary>
    public required int MaxConcurrentJobs { get; init; }
    
    /// <summary>
    /// Maximum job duration (null = no limit).
    /// </summary>
    public TimeSpan? MaxJobDuration { get; init; }
    
    /// <summary>
    /// Maximum queue wait time (null = no limit).
    /// </summary>
    public TimeSpan? MaxQueueWaitTime { get; init; }
    
    /// <summary>
    /// Number of jobs currently running in this queue.
    /// </summary>
    public required int RunningJobs { get; init; }
    
    /// <summary>
    /// Number of jobs waiting in this queue.
    /// </summary>
    public required int QueuedJobs { get; init; }
    
    /// <summary>
    /// IDs of jobs currently running in this queue.
    /// </summary>
    public IReadOnlyList<Guid> RunningJobIds { get; init; } = [];
    
    /// <summary>
    /// Whether this is the default queue.
    /// </summary>
    public bool IsDefault { get; init; }
}
