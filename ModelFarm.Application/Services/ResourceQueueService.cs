using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ModelFarm.Contracts.Resources;
using ModelFarm.Infrastructure.Persistence;
using ModelFarm.Infrastructure.Persistence.Entities;
using TorchSharp;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for managing resource queues.
/// </summary>
public sealed class ResourceQueueService : IResourceQueueService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;
    private readonly IResourceContainerService _containerService;
    
    /// <summary>
    /// Runtime state for each queue: semaphore and active jobs.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, QueueRuntimeState> _queueStates = new();
    
    /// <summary>
    /// Cached queue info for quick lookups.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, ResourceQueue> _queueCache = new();
    
    /// <summary>
    /// Lock for queue state initialization.
    /// </summary>
    private readonly SemaphoreSlim _initLock = new(1, 1);
    
    /// <summary>
    /// Detected hardware info (cached at startup).
    /// </summary>
    private readonly Lazy<DetectedHardware> _detectedHardware;

    public ResourceQueueService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        IResourceContainerService containerService)
    {
        _dbContextFactory = dbContextFactory;
        _containerService = containerService;
        _detectedHardware = new Lazy<DetectedHardware>(DetectHardware);
    }

    // ==================== Queue CRUD ====================

    public async Task<ResourceQueue> CreateQueueAsync(CreateQueueRequest request, CancellationToken cancellationToken = default)
    {
        // Validate that containers exist and are of correct type
        var cpuContainer = await _containerService.GetContainerAsync(request.CpuContainerId, cancellationToken);
        if (cpuContainer == null || cpuContainer.Type != ResourceType.CPU)
            throw new InvalidOperationException("Invalid CPU container specified.");
            
        var gpuContainer = await _containerService.GetContainerAsync(request.GpuContainerId, cancellationToken);
        if (gpuContainer == null || gpuContainer.Type != ResourceType.GPU)
            throw new InvalidOperationException("Invalid GPU container specified.");

        // Validate RAM container if specified
        if (request.RamContainerId.HasValue)
        {
            var ramContainer = await _containerService.GetContainerAsync(request.RamContainerId.Value, cancellationToken);
            if (ramContainer == null || ramContainer.Type != ResourceType.RAM)
                throw new InvalidOperationException("Invalid RAM container specified.");
        }

        var queue = new ResourceQueue
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CpuContainerId = request.CpuContainerId,
            GpuContainerId = request.GpuContainerId,
            RamContainerId = request.RamContainerId,
            MaxConcurrentJobs = Math.Max(1, request.MaxConcurrentJobs),
            MaxJobDuration = request.MaxJobDuration,
            MaxQueueWaitTime = request.MaxQueueWaitTime,
            Description = request.Description,
            IsDefault = request.IsDefault,
            CreatedAtUtc = DateTime.UtcNow
        };

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        // If this is marked as default, unset any existing default
        if (request.IsDefault)
        {
            await db.ResourceQueues
                .Where(q => q.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.IsDefault, false), cancellationToken);
        }

        db.ResourceQueues.Add(ResourceQueueEntity.FromContract(queue));
        await db.SaveChangesAsync(cancellationToken);

        // Initialize runtime state
        InitializeQueueState(queue);
        
        return queue;
    }

    public async Task<ResourceQueue?> GetQueueAsync(Guid queueId, CancellationToken cancellationToken = default)
    {
        // Check cache first
        if (_queueCache.TryGetValue(queueId, out var cached))
            return cached;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ResourceQueues.FindAsync([queueId], cancellationToken);
        if (entity == null)
            return null;

        var queue = entity.ToContract();
        _queueCache[queueId] = queue;
        return queue;
    }

    public async Task<IReadOnlyList<ResourceQueue>> GetAllQueuesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await db.ResourceQueues.OrderBy(q => q.Name).ToListAsync(cancellationToken);
        var queues = entities.Select(e => e.ToContract()).ToList();

        // Update cache
        foreach (var queue in queues)
        {
            _queueCache[queue.Id] = queue;
        }

        return queues;
    }

    public async Task<ResourceQueue?> GetDefaultQueueAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ResourceQueues.FirstOrDefaultAsync(q => q.IsDefault, cancellationToken);
        
        if (entity == null)
            return null;

        var queue = entity.ToContract();
        _queueCache[queue.Id] = queue;
        return queue;
    }

    public async Task<ResourceQueue?> UpdateQueueAsync(Guid queueId, UpdateQueueRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ResourceQueues.FindAsync([queueId], cancellationToken);
        if (entity == null)
            return null;

        if (request.Name != null)
            entity.Name = request.Name;
        if (request.CpuContainerId.HasValue)
        {
            var cpuContainer = await _containerService.GetContainerAsync(request.CpuContainerId.Value, cancellationToken);
            if (cpuContainer == null || cpuContainer.Type != ResourceType.CPU)
                throw new InvalidOperationException("Invalid CPU container specified.");
            entity.CpuContainerId = request.CpuContainerId.Value;
        }
        if (request.GpuContainerId.HasValue)
        {
            var gpuContainer = await _containerService.GetContainerAsync(request.GpuContainerId.Value, cancellationToken);
            if (gpuContainer == null || gpuContainer.Type != ResourceType.GPU)
                throw new InvalidOperationException("Invalid GPU container specified.");
            entity.GpuContainerId = request.GpuContainerId.Value;
        }
        if (request.MaxConcurrentJobs.HasValue)
            entity.MaxConcurrentJobs = Math.Max(1, request.MaxConcurrentJobs.Value);
        if (request.Description != null)
            entity.Description = request.Description;

        await db.SaveChangesAsync(cancellationToken);

        var queue = entity.ToContract();
        _queueCache[queueId] = queue;

        return queue;
    }

    public async Task<bool> DeleteQueueAsync(Guid queueId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.ResourceQueues.FindAsync([queueId], cancellationToken);
        if (entity == null)
            return false;

        // Cannot delete default queue
        if (entity.IsDefault)
            throw new InvalidOperationException("Cannot delete the default queue. Set another queue as default first.");

        // Check if any jobs are using this queue
        if (_queueStates.TryGetValue(queueId, out var state) && state.ActiveJobs.Count > 0)
            throw new InvalidOperationException($"Cannot delete queue with {state.ActiveJobs.Count} active jobs.");

        db.ResourceQueues.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        // Clean up runtime state
        _queueStates.TryRemove(queueId, out _);
        _queueCache.TryRemove(queueId, out _);

        return true;
    }

    // ==================== Resource Allocation ====================

    public async Task<bool> TryAcquireAsync(Guid queueId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateStateAsync(queueId, cancellationToken);
        if (state == null)
            throw new InvalidOperationException($"Queue {queueId} not found.");

        // Try to acquire without waiting
        if (state.Semaphore.Wait(0, cancellationToken))
        {
            state.ActiveJobs.TryAdd(jobId, DateTime.UtcNow);
            return true;
        }

        return false;
    }

    public async Task AcquireAsync(Guid queueId, Guid jobId, CancellationToken cancellationToken = default)
    {
        var state = await GetOrCreateStateAsync(queueId, cancellationToken);
        if (state == null)
            throw new InvalidOperationException($"Queue {queueId} not found.");

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

    public void Release(Guid queueId, Guid jobId)
    {
        if (_queueStates.TryGetValue(queueId, out var state))
        {
            if (state.ActiveJobs.TryRemove(jobId, out _))
            {
                state.Semaphore.Release();
            }
        }
    }

    // ==================== Status ====================

    public IReadOnlyList<ResourceQueueStatus> GetAllQueueStatus()
    {
        var result = new List<ResourceQueueStatus>();
        var containerStatus = _containerService.GetAllContainerStatus();

        foreach (var (queueId, state) in _queueStates)
        {
            if (_queueCache.TryGetValue(queueId, out var queue))
            {
                var cpuContainer = containerStatus.FirstOrDefault(c => c.ContainerId == queue.CpuContainerId);
                var gpuContainer = containerStatus.FirstOrDefault(c => c.ContainerId == queue.GpuContainerId);
                var ramContainer = queue.RamContainerId.HasValue 
                    ? containerStatus.FirstOrDefault(c => c.ContainerId == queue.RamContainerId.Value) 
                    : null;
                
                result.Add(new ResourceQueueStatus
                {
                    QueueId = queueId,
                    Name = queue.Name,
                    CpuContainerId = queue.CpuContainerId,
                    CpuContainerName = cpuContainer?.Name ?? "Unknown",
                    CpuContainerCapacity = (int)(cpuContainer?.MaxCapacity ?? 0),
                    GpuContainerId = queue.GpuContainerId,
                    GpuContainerName = gpuContainer?.Name ?? "Unknown",
                    GpuContainerCapacity = (int)(gpuContainer?.MaxCapacity ?? 0),
                    RamContainerId = queue.RamContainerId,
                    RamContainerName = ramContainer?.Name,
                    RamContainerCapacity = ramContainer?.MaxCapacity,
                    MaxConcurrentJobs = queue.MaxConcurrentJobs,
                    MaxJobDuration = queue.MaxJobDuration,
                    MaxQueueWaitTime = queue.MaxQueueWaitTime,
                    RunningJobs = state.ActiveJobs.Count,
                    QueuedJobs = state.QueuedJobs.Count,
                    RunningJobIds = state.ActiveJobs.Keys.ToList(),
                    IsDefault = queue.IsDefault
                });
            }
        }

        return result.OrderByDescending(s => s.IsDefault).ThenBy(s => s.Name).ToList();
    }

    public ResourceQueueStatus? GetQueueStatus(Guid queueId)
    {
        if (!_queueStates.TryGetValue(queueId, out var state))
            return null;

        if (!_queueCache.TryGetValue(queueId, out var queue))
            return null;

        var containerStatus = _containerService.GetAllContainerStatus();
        var cpuContainer = containerStatus.FirstOrDefault(c => c.ContainerId == queue.CpuContainerId);
        var gpuContainer = containerStatus.FirstOrDefault(c => c.ContainerId == queue.GpuContainerId);
        var ramContainer = queue.RamContainerId.HasValue 
            ? containerStatus.FirstOrDefault(c => c.ContainerId == queue.RamContainerId.Value) 
            : null;

        return new ResourceQueueStatus
        {
            QueueId = queueId,
            Name = queue.Name,
            CpuContainerId = queue.CpuContainerId,
            CpuContainerName = cpuContainer?.Name ?? "Unknown",
            CpuContainerCapacity = (int)(cpuContainer?.MaxCapacity ?? 0),
            GpuContainerId = queue.GpuContainerId,
            GpuContainerName = gpuContainer?.Name ?? "Unknown",
            GpuContainerCapacity = (int)(gpuContainer?.MaxCapacity ?? 0),
            RamContainerId = queue.RamContainerId,
            RamContainerName = ramContainer?.Name,
            RamContainerCapacity = ramContainer?.MaxCapacity,
            MaxConcurrentJobs = queue.MaxConcurrentJobs,
            MaxJobDuration = queue.MaxJobDuration,
            MaxQueueWaitTime = queue.MaxQueueWaitTime,
            RunningJobs = state.ActiveJobs.Count,
            QueuedJobs = state.QueuedJobs.Count,
            RunningJobIds = state.ActiveJobs.Keys.ToList(),
            IsDefault = queue.IsDefault
        };
    }

    public DetectedHardware GetDetectedHardware() => _detectedHardware.Value;

    // ==================== Initialization ====================

    public async Task EnsureDefaultQueueExistsAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // First ensure default containers exist
            await _containerService.EnsureDefaultContainersExistAsync(cancellationToken);
            
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existingQueues = await db.ResourceQueues.ToListAsync(cancellationToken);

            // Check for default queue
            if (!existingQueues.Any(q => q.IsDefault))
            {
                // Get the default containers
                var defaultCpu = await _containerService.GetDefaultContainerAsync(ResourceType.CPU, cancellationToken);
                var defaultGpu = await _containerService.GetDefaultContainerAsync(ResourceType.GPU, cancellationToken);
                
                if (defaultCpu != null && defaultGpu != null)
                {
                    var defaultQueue = new ResourceQueueEntity
                    {
                        Id = Guid.NewGuid(),
                        Name = "Default",
                        CpuContainerId = defaultCpu.Id,
                        GpuContainerId = defaultGpu.Id,
                        MaxConcurrentJobs = 1,  // Safe default
                        Description = $"Default queue using '{defaultCpu.Name}' + '{defaultGpu.Name}'",
                        IsDefault = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };
                    db.ResourceQueues.Add(defaultQueue);
                    Console.WriteLine($"[ResourceQueueService] Created default queue: '{defaultCpu.Name}' + '{defaultGpu.Name}', 1 concurrent job");
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            // Initialize runtime state for all queues
            var allQueues = await db.ResourceQueues.ToListAsync(cancellationToken);
            foreach (var entity in allQueues)
            {
                var queue = entity.ToContract();
                InitializeQueueState(queue);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    // ==================== Private Helpers ====================

    private void InitializeQueueState(ResourceQueue queue)
    {
        _queueCache[queue.Id] = queue;
        _queueStates.GetOrAdd(queue.Id, _ => new QueueRuntimeState(queue.MaxConcurrentJobs));
    }

    private async Task<QueueRuntimeState?> GetOrCreateStateAsync(Guid queueId, CancellationToken cancellationToken)
    {
        if (_queueStates.TryGetValue(queueId, out var state))
            return state;

        // Load from database and initialize
        var queue = await GetQueueAsync(queueId, cancellationToken);
        if (queue == null)
            return null;

        InitializeQueueState(queue);
        return _queueStates.GetValueOrDefault(queueId);
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
                gpuNames.Add($"GPU {i}");
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
    /// Runtime state for a queue.
    /// </summary>
    private sealed class QueueRuntimeState
    {
        public SemaphoreSlim Semaphore { get; }
        public ConcurrentDictionary<Guid, DateTime> ActiveJobs { get; } = new();
        public ConcurrentDictionary<Guid, DateTime> QueuedJobs { get; } = new();

        public QueueRuntimeState(int maxConcurrentJobs)
        {
            Semaphore = new SemaphoreSlim(maxConcurrentJobs, maxConcurrentJobs);
        }
    }
}
