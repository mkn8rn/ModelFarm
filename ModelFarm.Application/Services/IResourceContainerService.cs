using ModelFarm.Contracts.Resources;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for managing resource containers and coordinating resource allocation.
/// </summary>
public interface IResourceContainerService
{
    // ==================== Container CRUD ====================
    
    /// <summary>
    /// Creates a new resource container.
    /// </summary>
    Task<ResourceContainer> CreateContainerAsync(CreateContainerRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a container by ID.
    /// </summary>
    Task<ResourceContainer?> GetContainerAsync(Guid containerId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all containers, optionally filtered by type.
    /// </summary>
    Task<IReadOnlyList<ResourceContainer>> GetAllContainersAsync(ResourceType? typeFilter = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the default container for a resource type.
    /// </summary>
    Task<ResourceContainer?> GetDefaultContainerAsync(ResourceType type, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a container's settings.
    /// </summary>
    Task<ResourceContainer?> UpdateContainerAsync(Guid containerId, UpdateContainerRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Deletes a container. Cannot delete default containers or containers with active jobs.
    /// </summary>
    Task<bool> DeleteContainerAsync(Guid containerId, CancellationToken cancellationToken = default);
    
    // ==================== Resource Allocation ====================
    
    /// <summary>
    /// Attempts to acquire resources from a container for a job.
    /// Returns true if resources were acquired, false if the container is at capacity.
    /// </summary>
    Task<bool> TryAcquireAsync(Guid containerId, Guid jobId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Acquires resources from a container, waiting if necessary.
    /// </summary>
    Task AcquireAsync(Guid containerId, Guid jobId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Releases resources back to a container.
    /// </summary>
    void Release(Guid containerId, Guid jobId);
    
    // ==================== Status ====================
    
    /// <summary>
    /// Gets the runtime status of all containers.
    /// </summary>
    IReadOnlyList<ResourceContainerStatus> GetAllContainerStatus();
    
    /// <summary>
    /// Gets the runtime status of a specific container.
    /// </summary>
    ResourceContainerStatus? GetContainerStatus(Guid containerId);
    
    /// <summary>
    /// Gets detected hardware information.
    /// </summary>
    DetectedHardware GetDetectedHardware();
    
    // ==================== Initialization ====================
    
    /// <summary>
    /// Ensures default containers exist. Called at startup.
    /// </summary>
    Task EnsureDefaultContainersExistAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Request to create a new resource container.
/// </summary>
public sealed record CreateContainerRequest
{
    public required string Name { get; init; }
    public required ResourceType Type { get; init; }
    public required long MaxCapacity { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
}

/// <summary>
/// Request to update a resource container.
/// </summary>
public sealed record UpdateContainerRequest
{
    public string? Name { get; init; }
    public long? MaxCapacity { get; init; }
    public string? Description { get; init; }
}
