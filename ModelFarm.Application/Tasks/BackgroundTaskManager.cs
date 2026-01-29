using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelFarm.Contracts.Tasks;
using ModelFarm.Infrastructure.Persistence;
using ModelFarm.Infrastructure.Persistence.Entities;

namespace ModelFarm.Application.Tasks;

/// <summary>
/// Centralized manager for all background tasks.
/// Maintains task queue, state, and cancellation tokens with database persistence.
/// Uses a write-behind queue to batch database updates efficiently.
/// </summary>
public sealed class BackgroundTaskManager : IBackgroundTaskManager, IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, BackgroundTask> _tasks = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationTokens = new();
    private readonly SemaphoreSlim _taskSignal = new(0);
    private readonly object _queueLock = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundTaskManager> _logger;
    private bool _initialized;

    // Write-behind queue for batched persistence
    private readonly Channel<Guid> _persistenceQueue = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, byte> _pendingPersistence = new();
    private readonly Task _persistenceTask;
    private readonly CancellationTokenSource _disposalCts = new();

    public BackgroundTaskManager(IServiceScopeFactory scopeFactory, ILogger<BackgroundTaskManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _persistenceTask = ProcessPersistenceQueueAsync(_disposalCts.Token);
    }

    public BackgroundTask ScheduleTask(
        BackgroundTaskType type,
        string name,
        TaskParameters parameters,
        Guid? relatedEntityId = null,
        int priority = 100)
    {
        var task = new BackgroundTask
        {
            Id = Guid.NewGuid(),
            Type = type,
            Name = name,
            Status = BackgroundTaskStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            ParametersJson = parameters.ToJson(),
            RelatedEntityId = relatedEntityId,
            Priority = priority
        };

        var cts = new CancellationTokenSource();
        _tasks[task.Id] = task;
        _cancellationTokens[task.Id] = cts;

        QueuePersistence(task.Id);
        SignalNewTask();
        return task;
    }

    public BackgroundTask? GetTask(Guid taskId)
    {
        return _tasks.TryGetValue(taskId, out var task) ? task : null;
    }

    public IReadOnlyList<BackgroundTask> GetTasks(BackgroundTaskStatus? status = null)
    {
        var tasks = _tasks.Values.AsEnumerable();
        
        if (status.HasValue)
            tasks = tasks.Where(t => t.Status == status.Value);

        return tasks
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.CreatedAtUtc)
            .ToList();
    }

    public IReadOnlyList<BackgroundTask> GetTasksForEntity(Guid entityId)
    {
        return _tasks.Values
            .Where(t => t.RelatedEntityId == entityId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToList();
    }

    public bool CancelTask(Guid taskId)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
            return false;

        if (task.Status is BackgroundTaskStatus.Completed or BackgroundTaskStatus.Failed or BackgroundTaskStatus.Cancelled)
            return false;

        if (_cancellationTokens.TryGetValue(taskId, out var cts))
        {
            cts.Cancel();
        }

        task.Status = BackgroundTaskStatus.Cancelled;
        task.CompletedAtUtc = DateTime.UtcNow;
        task.ProgressMessage = "Cancelled by user";

        QueuePersistence(taskId);
        return true;
    }

    public BackgroundTask? DequeueNextTask()
    {
        lock (_queueLock)
        {
            var nextTask = _tasks.Values
                .Where(t => t.Status == BackgroundTaskStatus.Pending)
                .OrderBy(t => t.Priority)
                .ThenBy(t => t.CreatedAtUtc)
                .FirstOrDefault();

            if (nextTask is not null)
            {
                nextTask.Status = BackgroundTaskStatus.Running;
                nextTask.StartedAtUtc = DateTime.UtcNow;
                QueuePersistence(nextTask.Id);
            }

            return nextTask;
        }
    }

    public void UpdateTaskProgress(Guid taskId, int progressPercent, string? message, long current = 0, long total = 0)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.ProgressPercent = progressPercent;
            task.ProgressMessage = message;
            task.ProgressCurrent = current;
            task.ProgressTotal = total;

            // Throttle progress updates to database (every 25%)
            if (progressPercent % 25 == 0)
            {
                QueuePersistence(taskId);
            }
        }
    }

    public void CompleteTask(Guid taskId, TaskResult? result = null)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Status = BackgroundTaskStatus.Completed;
            task.CompletedAtUtc = DateTime.UtcNow;
            task.ProgressPercent = 100;
            task.ResultJson = result?.ToJson();

            QueuePersistence(taskId);
        }

        CleanupCancellationToken(taskId);
    }

    public void FailTask(Guid taskId, string errorMessage)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Status = BackgroundTaskStatus.Failed;
            task.CompletedAtUtc = DateTime.UtcNow;
            task.ErrorMessage = errorMessage;

            QueuePersistence(taskId);
        }

        CleanupCancellationToken(taskId);
    }

    public CancellationToken GetCancellationToken(Guid taskId)
    {
        return _cancellationTokens.TryGetValue(taskId, out var cts) 
            ? cts.Token 
            : CancellationToken.None;
    }

    public void SignalNewTask()
    {
        _taskSignal.Release();
    }

    public async Task WaitForTasksAsync(CancellationToken cancellationToken)
    {
        // Initialize from database on first wait
        if (!_initialized)
        {
            await InitializeFromDatabaseAsync(cancellationToken);
            _initialized = true;
        }

        await _taskSignal.WaitAsync(cancellationToken);
    }

    private void CleanupCancellationToken(Guid taskId)
    {
        if (_cancellationTokens.TryRemove(taskId, out var cts))
        {
            cts.Dispose();
        }
    }

    private void QueuePersistence(Guid taskId)
    {
        // Deduplicate: only queue if not already pending
        if (_pendingPersistence.TryAdd(taskId, 0))
        {
            _persistenceQueue.Writer.TryWrite(taskId);
        }
    }

    private async Task ProcessPersistenceQueueAsync(CancellationToken cancellationToken)
    {
        var batch = new List<Guid>(10);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                batch.Clear();

                // Wait for first item
                if (await _persistenceQueue.Reader.WaitToReadAsync(cancellationToken))
                {
                    // Collect batch (up to 10 items or 100ms timeout)
                    using var batchCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, batchCts.Token);

                    while (batch.Count < 10 && _persistenceQueue.Reader.TryRead(out var taskId))
                    {
                        _pendingPersistence.TryRemove(taskId, out _);
                        batch.Add(taskId);
                    }

                    if (batch.Count > 0)
                    {
                        await PersistBatchAsync(batch, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in persistence queue processor");
                await Task.Delay(1000, cancellationToken);
            }
        }

        // Flush remaining items on shutdown
        while (_persistenceQueue.Reader.TryRead(out var taskId))
        {
            batch.Add(taskId);
        }
        if (batch.Count > 0)
        {
            await PersistBatchAsync(batch, CancellationToken.None);
        }
    }

    private async Task PersistBatchAsync(List<Guid> taskIds, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            foreach (var taskId in taskIds)
            {
                if (!_tasks.TryGetValue(taskId, out var task))
                    continue;

                var entity = await db.BackgroundTasks.FindAsync([taskId], cancellationToken);
                if (entity is null)
                {
                    entity = BackgroundTaskEntity.FromTask(task);
                    db.BackgroundTasks.Add(entity);
                }
                else
                {
                    entity.UpdateFrom(task);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Persisted {Count} tasks to database", taskIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist batch of {Count} tasks", taskIds.Count);
        }
    }

    private async Task InitializeFromDatabaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

            // Load recent tasks (last 24 hours or incomplete)
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var entities = await db.BackgroundTasks
                .Where(t => t.CreatedAtUtc > cutoff || 
                           t.Status == BackgroundTaskStatus.Pending || 
                           t.Status == BackgroundTaskStatus.Running)
                .ToListAsync(cancellationToken);

            foreach (var entity in entities)
            {
                var task = entity.ToTask();

                // Reset running tasks to pending (they were interrupted)
                if (task.Status == BackgroundTaskStatus.Running)
                {
                    task.Status = BackgroundTaskStatus.Pending;
                    task.StartedAtUtc = null;
                }

                _tasks[task.Id] = task;
                _cancellationTokens[task.Id] = new CancellationTokenSource();

                if (task.Status == BackgroundTaskStatus.Pending)
                {
                    _taskSignal.Release();
                }
            }

            _logger.LogInformation("Loaded {Count} tasks from database ({Pending} pending)", 
                entities.Count, 
                entities.Count(e => e.Status == BackgroundTaskStatus.Pending || e.Status == BackgroundTaskStatus.Running));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize tasks from database");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposalCts.Cancel();
        _persistenceQueue.Writer.Complete();
        
        try
        {
            await _persistenceTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Persistence task did not complete in time");
        }
        
        _disposalCts.Dispose();
    }
}
