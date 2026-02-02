using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.Resources;

namespace ModelFarm.Infrastructure.Persistence.Entities;

/// <summary>
/// Database entity for resource queues.
/// A queue pairs a CPU container with a GPU container and optionally a RAM container.
/// </summary>
[Index(nameof(Name), IsUnique = true)]
public sealed class ResourceQueueEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required Guid CpuContainerId { get; set; }
    public required Guid GpuContainerId { get; set; }
    public Guid? RamContainerId { get; set; }
    public required int MaxConcurrentJobs { get; set; }
    public TimeSpan? MaxJobDuration { get; set; }
    public TimeSpan? MaxQueueWaitTime { get; set; }
    public bool IsDefault { get; set; }
    public required DateTime CreatedAtUtc { get; set; }

    public ResourceQueue ToContract() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        CpuContainerId = CpuContainerId,
        GpuContainerId = GpuContainerId,
        RamContainerId = RamContainerId,
        MaxConcurrentJobs = MaxConcurrentJobs,
        MaxJobDuration = MaxJobDuration,
        MaxQueueWaitTime = MaxQueueWaitTime,
        IsDefault = IsDefault,
        CreatedAtUtc = CreatedAtUtc
    };

    public static ResourceQueueEntity FromContract(ResourceQueue queue) => new()
    {
        Id = queue.Id,
        Name = queue.Name,
        Description = queue.Description,
        CpuContainerId = queue.CpuContainerId,
        GpuContainerId = queue.GpuContainerId,
        RamContainerId = queue.RamContainerId,
        MaxConcurrentJobs = queue.MaxConcurrentJobs,
        MaxJobDuration = queue.MaxJobDuration,
        MaxQueueWaitTime = queue.MaxQueueWaitTime,
        IsDefault = queue.IsDefault,
        CreatedAtUtc = queue.CreatedAtUtc
    };
}
