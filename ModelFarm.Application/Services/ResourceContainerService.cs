using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.Resources;
using ModelFarm.Infrastructure.Persistence;
using ModelFarm.Infrastructure.Persistence.Entities;
using TorchSharp;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for managing resource containers and coordinating resource allocation.
/// </summary>
public sealed class ResourceContainerService : IResourceContainerService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    
    /// <summary>
    /// Runtime state for each container: semaphore and active jobs.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, ContainerRuntimeState> _containerStates = new();
    
    /// <summary>
    /// Cached container info for quick lookups.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, ResourceContainer> _containerCache = new();
    
    /// <summary>
    /// Lock for container state initialization.
    /// </summary>
    private readonly SemaphoreSlim _initLock = new(1, 1);
    
    /// <summary>
    /// Detected hardware info (cached at startup).
    /// </summary>
    private readonly Lazy<DetectedHardware> _detectedHardware;

    public ResourceContainerService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
        _detectedHardware = new Lazy<DetectedHardware>(DetectHardware);
    }

    // ==================== Container CRUD ====================

    public async Task<ResourceContainer> CreateContainerAsync(CreateContainerRequest request, CancellationToken cancellationToken = default)
    {
        // For RAM containers, capacity is in bytes (minimum 1MB)
        var minCapacity = request.Type == ResourceType.RAM ? 1_000_000L : 1L;
        
        var container = new ResourceContainer
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            MaxCapacity = Math.Max(minCapacity, request.MaxCapacity),
            Description = request.Description,
            IsDefault = request.IsDefault,
            CreatedAtUtc = DateTime.UtcNow
        };

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        // If this is marked as default, unset any existing default for this type
        if (request.IsDefault)
        {
            var existingDefault = await db.ResourceContainers
                .FirstOrDefaultAsync(c => c.Type == request.Type && c.IsDefault, cancellationToken);
            if (existingDefault != null)
            {
                existingDefault.IsDefault = false;
                
                // Update the cache to reflect the change
                if (_containerCache.TryGetValue(existingDefault.Id, out var cachedContainer))
                {
                    _containerCache[existingDefault.Id] = cachedContainer with { IsDefault = false };
                }
            }
        }

        db.ResourceContainers.Add(ResourceContainerEntity.FromContract(container));
        await db.SaveChangesAsync(cancellationToken);

        // Initialize runtime state
        InitializeContainerState(container);
        
        return container;
    }

    public async Task<ResourceContainer?> GetContainerAsync(Guid containerId, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_containerCache.TryGetValue(containerId, out var cached))
            return cached;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ResourceContainers.FindAsync([containerId], cancellationToken);
        if (entity == null)
            return null;

        var container = entity.ToContract();
        _containerCache[containerId] = container;
        return container;
    }

    public async Task<IReadOnlyList<ResourceContainer>> GetAllContainersAsync(ResourceType? typeFilter = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        var query = db.ResourceContainers.AsQueryable();
        if (typeFilter.HasValue)
            query = query.Where(c => c.Type == typeFilter.Value);

        var entities = await query.OrderBy(c => c.Type).ThenBy(c => c.Name).ToListAsync(cancellationToken);
        var containers = entities.Select(e => e.ToContract()).ToList();

        // Update cache
        foreach (var container in containers)
        {
            _containerCache[container.Id] = container;
        }

        return containers;
    }

    public async Task<ResourceContainer?> GetDefaultContainerAsync(ResourceType type, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ResourceContainers
            .FirstOrDefaultAsync(c => c.Type == type && c.IsDefault, cancellationToken);
        
        if (entity == null)
            return null;

        var container = entity.ToContract();
        _containerCache[container.Id] = container;
        return container;
    }

    public async Task<ResourceContainer?> UpdateContainerAsync(Guid containerId, UpdateContainerRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ResourceContainers.FindAsync([containerId], cancellationToken);
        if (entity == null)
            return null;

        if (request.Name != null)
            entity.Name = request.Name;
        if (request.MaxCapacity.HasValue)
            entity.MaxCapacity = Math.Max(1, request.MaxCapacity.Value);
        if (request.Description != null)
            entity.Description = request.Description;

        await db.SaveChangesAsync(cancellationToken);

        var container = entity.ToContract();
        _containerCache[containerId] = container;

        // Update semaphore if capacity changed
        if (request.MaxCapacity.HasValue && _containerStates.TryGetValue(containerId, out var state))
        {
            // Note: Changing semaphore capacity at runtime is complex.
            // For now, changes take effect on next app restart.
            // A more sophisticated implementation would recreate the semaphore.
        }

        return container;
    }

    public async Task<bool> DeleteContainerAsync(Guid containerId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ResourceContainers.FindAsync([containerId], cancellationToken);
        if (entity == null)
            return false;

        // Cannot delete default containers
        if (entity.IsDefault)
            throw new InvalidOperationException("Cannot delete the default container. Set another container as default first.");

        // Check if any jobs are using this container
        if (_containerStates.TryGetValue(containerId, out var state) && state.ActiveJobs.Count > 0)
            throw new InvalidOperationException($"Cannot delete container with {state.ActiveJobs.Count} active jobs.");

        db.ResourceContainers.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        // Clean up runtime state
        _containerStates.TryRemove(containerId, out _);
        _containerCache.TryRemove(containerId, out _);

        return true;
    }

    // ==================== Resource Allocation ====================

    public async Task<bool> TryAcquireAsync(Guid containerId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateStateAsync(containerId, cancellationToken);
        if (state == null)
            throw new InvalidOperationException($"Container {containerId} not found.");

        // Try to acquire without waiting
        if (state.Semaphore.Wait(0, cancellationToken))
        {
            state.ActiveJobs.TryAdd(jobId, DateTime.UtcNow);
            return true;
        }

        return false;
    }

    public async Task AcquireAsync(Guid containerId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateStateAsync(containerId, cancellationToken);
        if (state == null)
            throw new InvalidOperationException($"Container {containerId} not found.");

        // Track as queued while waiting
        state.QueuedJobs.TryAdd(jobId, DateTime.UtcNow);
        
        try
        {
            await state.Semaphore.WaitAsync(cancellationToken);
            state.ActiveJobs.TryAdd(jobId, DateTime.UtcNow);
        }
        finally
        {
            state.QueuedJobs.TryRemove(jobId, out _);
        }
    }

    public void Release(Guid containerId, Guid jobId)
    {
        if (_containerStates.TryGetValue(containerId, out var state))
        {
            state.ActiveJobs.TryRemove(jobId, out _);
            state.Semaphore.Release();
        }
    }

    // ==================== Status ====================

    public IReadOnlyList<ResourceContainerStatus> GetAllContainerStatus()
    {
        var result = new List<ResourceContainerStatus>();

        foreach (var (containerId, state) in _containerStates)
        {
            if (_containerCache.TryGetValue(containerId, out var container))
            {
                result.Add(new ResourceContainerStatus
                {
                    ContainerId = containerId,
                    Name = container.Name,
                    Type = container.Type,
                    MaxCapacity = container.MaxCapacity,
                    CurrentUsage = state.ActiveJobs.Count,
                    QueuedJobs = state.QueuedJobs.Count,
                    ActiveJobIds = state.ActiveJobs.Keys.ToList(),
                    IsDefault = container.IsDefault,
                    Description = container.Description
                });
            }
        }

        return result.OrderBy(s => s.Type).ThenBy(s => s.Name).ToList();
    }

    public ResourceContainerStatus? GetContainerStatus(Guid containerId)
    {
        if (!_containerStates.TryGetValue(containerId, out var state))
            return null;

        if (!_containerCache.TryGetValue(containerId, out var container))
            return null;

        return new ResourceContainerStatus
        {
            ContainerId = containerId,
            Name = container.Name,
            Type = container.Type,
            MaxCapacity = container.MaxCapacity,
            CurrentUsage = state.ActiveJobs.Count,
            QueuedJobs = state.QueuedJobs.Count,
            ActiveJobIds = state.ActiveJobs.Keys.ToList(),
            IsDefault = container.IsDefault,
            Description = container.Description
        };
    }

    public DetectedHardware GetDetectedHardware() => _detectedHardware.Value;

    // ==================== Initialization ====================

    public async Task EnsureDefaultContainersExistAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existingContainers = await db.ResourceContainers.ToListAsync(cancellationToken);

            // Check for default CPU container
            if (!existingContainers.Any(c => c.Type == ResourceType.CPU && c.IsDefault))
            {
                var cpuCores = Environment.ProcessorCount;
                var defaultCpuCapacity = Math.Max(2, cpuCores / 2); // Use half of available cores by default

                var cpuContainer = new ResourceContainerEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Default CPU",
                    Type = ResourceType.CPU,
                    MaxCapacity = defaultCpuCapacity,
                    Description = $"Default CPU container with {defaultCpuCapacity} threads (detected {cpuCores} cores)",
                    IsDefault = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.ResourceContainers.Add(cpuContainer);
                Console.WriteLine($"[ResourceContainerService] Created default CPU container with {defaultCpuCapacity} threads");
            }

            // Check for default GPU container
            if (!existingContainers.Any(c => c.Type == ResourceType.GPU && c.IsDefault))
            {
                var gpuCount = torch.cuda.is_available() ? (int)torch.cuda.device_count() : 0;
                var defaultGpuCapacity = Math.Max(1, gpuCount); // Use all available GPUs

                var gpuContainer = new ResourceContainerEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Default GPU",
                    Type = ResourceType.GPU,
                    MaxCapacity = defaultGpuCapacity,
                    Description = gpuCount > 0 
                        ? $"Default GPU container with {defaultGpuCapacity} device(s)" 
                        : "Default GPU container (no GPU detected, will use CPU)",
                    IsDefault = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                db.ResourceContainers.Add(gpuContainer);
                Console.WriteLine($"[ResourceContainerService] Created default GPU container with {defaultGpuCapacity} device(s)");
            }

            await db.SaveChangesAsync(cancellationToken);

            // Initialize runtime state for all containers
            var allContainers = await db.ResourceContainers.ToListAsync(cancellationToken);
            foreach (var entity in allContainers)
            {
                var container = entity.ToContract();
                InitializeContainerState(container);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ==================== Private Helpers ====================

    private void InitializeContainerState(ResourceContainer container)
    {
        _containerCache[container.Id] = container;
        _containerStates.GetOrAdd(container.Id, _ => new ContainerRuntimeState(container.MaxCapacity));
    }

    private async Task<ContainerRuntimeState?> GetOrCreateStateAsync(Guid containerId, CancellationToken cancellationToken)
    {
        if (_containerStates.TryGetValue(containerId, out var state))
            return state;

        // Load from database and initialize
        var container = await GetContainerAsync(containerId, cancellationToken);
        if (container == null)
            return null;

        InitializeContainerState(container);
        return _containerStates.GetValueOrDefault(containerId);
    }

    private static DetectedHardware DetectHardware()
    {
        var cpuCores = Environment.ProcessorCount;
        var cudaAvailable = torch.cuda.is_available();
        var gpuCount = cudaAvailable ? (int)torch.cuda.device_count() : 0;
        
        var gpuNames = new List<string>();
        if (cudaAvailable)
        {
            for (int i = 0; i < gpuCount; i++)
            {
                try
                {
                    gpuNames.Add($"GPU {i}"); // TorchSharp doesn't expose device names easily
                }
                catch
                {
                    gpuNames.Add($"GPU {i} (unknown)");
                }
            }
        }

        // Get RAM info
        var gcInfo = GC.GetGCMemoryInfo();
        var totalRam = gcInfo.TotalAvailableMemoryBytes;
        var availableRam = totalRam - GC.GetTotalMemory(false);

        return new DetectedHardware
        {
            CpuCores = cpuCores,
            GpuDevices = gpuCount,
            GpuNames = gpuNames,
            CudaAvailable = cudaAvailable,
            TotalRamBytes = totalRam,
            AvailableRamBytes = availableRam
        };
    }

    /// <summary>
    /// Runtime state for a container.
    /// </summary>
    private sealed class ContainerRuntimeState
    {
        public SemaphoreSlim Semaphore { get; }
        public ConcurrentDictionary<Guid, DateTime> ActiveJobs { get; } = new();
        public ConcurrentDictionary<Guid, DateTime> QueuedJobs { get; } = new();

        public ContainerRuntimeState(long maxCapacity)
        {
            // For CPU/GPU, maxCapacity is small (threads/devices)
            // For RAM containers, we use 1 as semaphore (memory tracking is done separately)
            var semaphoreCount = maxCapacity > int.MaxValue ? 1 : Math.Max(1, (int)maxCapacity);
            Semaphore = new SemaphoreSlim(semaphoreCount, semaphoreCount);
        }
    }
}
