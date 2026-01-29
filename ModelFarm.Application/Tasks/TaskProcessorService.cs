using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelFarm.Contracts.Tasks;

namespace ModelFarm.Application.Tasks;

/// <summary>
/// Background service that processes tasks from the task manager.
/// Each task runs on its own dedicated thread for isolation.
/// </summary>
public sealed class TaskProcessorService : BackgroundService
{
    private readonly IBackgroundTaskManager _taskManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskProcessorService> _logger;
    private readonly int _maxConcurrency;

    public TaskProcessorService(
        IBackgroundTaskManager taskManager,
        IServiceScopeFactory scopeFactory,
        ILogger<TaskProcessorService> logger,
        int maxConcurrency = 4)
    {
        _taskManager = taskManager;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _maxConcurrency = maxConcurrency;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task processor service starting with max concurrency: {MaxConcurrency}", _maxConcurrency);

        using var semaphore = new SemaphoreSlim(_maxConcurrency);
        var runningTasks = new List<Task>();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for a task to be available
                await _taskManager.WaitForTasksAsync(stoppingToken);

                // Try to dequeue and process tasks up to max concurrency
                while (true)
                {
                    // Clean up completed tasks
                    runningTasks.RemoveAll(t => t.IsCompleted);

                    // Check if we can run more tasks
                    if (!await semaphore.WaitAsync(0, stoppingToken))
                        break;

                    var task = _taskManager.DequeueNextTask();
                    if (task is null)
                    {
                        semaphore.Release();
                        break;
                    }

                    // Start processing the task on its own dedicated thread
                    var processingTask = Task.Factory.StartNew(
                        () => ProcessTaskOnDedicatedThread(task, semaphore, stoppingToken),
                        stoppingToken,
                        TaskCreationOptions.LongRunning,  // Hints scheduler to use dedicated thread
                        TaskScheduler.Default).Unwrap();
                    
                    runningTasks.Add(processingTask);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in task processor loop");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        // Wait for running tasks to complete
        if (runningTasks.Count > 0)
        {
            _logger.LogInformation("Waiting for {Count} running tasks to complete...", runningTasks.Count);
            await Task.WhenAll(runningTasks);
        }

        _logger.LogInformation("Task processor service stopped");
    }

    private async Task ProcessTaskOnDedicatedThread(BackgroundTask task, SemaphoreSlim semaphore, CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting task {TaskId} ({TaskType}): {TaskName} on thread {ThreadId}", 
                task.Id, task.Type, task.Name, Environment.CurrentManagedThreadId);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var handlers = scope.ServiceProvider.GetServices<IBackgroundTaskHandler>();
            var handler = handlers.FirstOrDefault(h => h.TaskType == task.Type);

            if (handler is null)
            {
                _logger.LogError("No handler found for task type {TaskType}", task.Type);
                _taskManager.FailTask(task.Id, $"No handler found for task type {task.Type}");
                return;
            }

            var taskCancellation = _taskManager.GetCancellationToken(task.Id);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(taskCancellation, stoppingToken);

            var progress = new Progress<TaskProgressUpdate>(update =>
            {
                _taskManager.UpdateTaskProgress(task.Id, update.ProgressPercent, update.Message, update.Current, update.Total);
            });

            await handler.ExecuteAsync(task, progress, linkedCts.Token);

            // Mark task as completed
            _taskManager.CompleteTask(task.Id);
            _logger.LogInformation("Task {TaskId} completed successfully", task.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Task {TaskId} was cancelled", task.Id);
            _taskManager.FailTask(task.Id, "Task was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} failed with error", task.Id);
            _taskManager.FailTask(task.Id, ex.Message);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
