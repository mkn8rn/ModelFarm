using ModelFarm.Contracts.Resources;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for managing resource queues.
/// Queues pair a CPU container with a GPU container for simpler job management.
/// </summary>
public interface IResourceQueueService
{
    // ==================== Queue CRUD ====================
    
    /// <summary>
    /// Creates a new resource queue by pairing containers.
    /// </summary>
    Task<ResourceQueue> CreateQueueAsync(CreateQueueRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a queue by ID.
    /// </summary>
    Task<ResourceQueue?> GetQueueAsync(Guid queueId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all queues.
    /// </summary>
    Task<IReadOnlyList<ResourceQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the default queue.
    /// </summary>
    Task<ResourceQueue?> GetDefaultQueueAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a queue's settings.
    /// </summary>
    Task<ResourceQueue?> UpdateQueueAsync(Guid queueId, UpdateQueueRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a queue. Cannot delete default queue or queues with active jobs.
    /// </summary>
    Task<bool> DeleteQueueAsync(Guid queueId, CancellationToken cancellationToken = default);
    
    // ==================== Resource Allocation ====================
    
    /// <summary>
    /// Attempts to acquire a slot in a queue for a job.
    /// Returns true if acquired, false if at capacity.
    /// </summary>
    Task<bool> TryAcquireAsync(Guid queueId, Guid jobId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Acquires a slot in a queue, waiting if necessary.
    /// </summary>
    Task AcquireAsync(Guid queueId, Guid jobId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Releases a slot back to a queue.
    /// </summary>
    void Release(Guid queueId, Guid jobId);
    
    // ==================== Status ====================
    
    /// <summary>
    /// Gets the runtime status of all queues.
    /// </summary>
    IReadOnlyList<ResourceQueueStatus> GetAllQueueStatus();
    
    /// <summary>
    /// Gets the runtime status of a specific queue.
    /// </summary>
    ResourceQueueStatus? GetQueueStatus(Guid queueId);
    
    /// <summary>
    /// Gets detected hardware information.
    /// </summary>
    DetectedHardware GetDetectedHardware();
    
    // ==================== Initialization ====================
    
    /// <summary>
    /// Ensures the default queue exists. Called at startup.
    /// </summary>
    Task EnsureDefaultQueueExistsAsync(CancellationToken cancellationToken = default);
}


/// <summary>
/// Request to create a new resource queue by pairing containers.
/// </summary>
public sealed record CreateQueueRequest
{
    public required string Name { get; init; }
    public required Guid CpuContainerId { get; init; }
    public required Guid GpuContainerId { get; init; }
    public Guid? RamContainerId { get; init; }
    public required int MaxConcurrentJobs { get; init; }
    public TimeSpan? MaxJobDuration { get; init; }
    public TimeSpan? MaxQueueWaitTime { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
}

/// <summary>
/// Request to update a resource queue.
/// </summary>
public sealed record UpdateQueueRequest
{
    public string? Name { get; init; }
    public Guid? CpuContainerId { get; init; }
    public Guid? GpuContainerId { get; init; }
    public Guid? RamContainerId { get; init; }
    public int? MaxConcurrentJobs { get; init; }
    public TimeSpan? MaxJobDuration { get; init; }
    public TimeSpan? MaxQueueWaitTime { get; init; }
    public string? Description { get; init; }
}
