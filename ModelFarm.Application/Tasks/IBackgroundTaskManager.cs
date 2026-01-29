using ModelFarm.Contracts.Tasks;

namespace ModelFarm.Application.Tasks;

/// <summary>
/// Interface for the centralized background task manager.
/// All services should use this to schedule and query background tasks.
/// </summary>
public interface IBackgroundTaskManager
{
    /// <summary>
    /// Schedules a new background task for execution.
    /// </summary>
    /// <param name="type">The type of task to execute.</param>
    /// <param name="name">Human-readable name for the task.</param>
    /// <param name="parameters">Task-specific parameters.</param>
    /// <param name="relatedEntityId">Optional ID of a related entity (e.g., DatasetId).</param>
    /// <param name="priority">Task priority (lower = higher priority).</param>
    /// <returns>The scheduled task.</returns>
    BackgroundTask ScheduleTask(
        BackgroundTaskType type,
        string name,
        TaskParameters parameters,
        Guid? relatedEntityId = null,
        int priority = 100);

    /// <summary>
    /// Gets a task by its ID.
    /// </summary>
    BackgroundTask? GetTask(Guid taskId);

    /// <summary>
    /// Gets all tasks, optionally filtered by status.
    /// </summary>
    IReadOnlyList<BackgroundTask> GetTasks(BackgroundTaskStatus? status = null);

    /// <summary>
    /// Gets tasks related to a specific entity.
    /// </summary>
    IReadOnlyList<BackgroundTask> GetTasksForEntity(Guid entityId);

    /// <summary>
    /// Cancels a pending or running task.
    /// </summary>
    bool CancelTask(Guid taskId);

    /// <summary>
    /// Gets the next pending task for execution (used by the processor).
    /// </summary>
    BackgroundTask? DequeueNextTask();

    /// <summary>
    /// Updates a task's status and progress (used by handlers).
    /// </summary>
    void UpdateTaskProgress(Guid taskId, int progressPercent, string? message, long current = 0, long total = 0);

    /// <summary>
    /// Marks a task as completed with a result (used by handlers).
    /// </summary>
    void CompleteTask(Guid taskId, TaskResult? result = null);

    /// <summary>
    /// Marks a task as failed with an error message (used by handlers).
    /// </summary>
    void FailTask(Guid taskId, string errorMessage);

    /// <summary>
    /// Gets the cancellation token for a specific task.
    /// </summary>
    CancellationToken GetCancellationToken(Guid taskId);

    /// <summary>
    /// Signals that new tasks are available for processing.
    /// </summary>
    void SignalNewTask();

    /// <summary>
    /// Waits for new tasks to be available.
    /// </summary>
    Task WaitForTasksAsync(CancellationToken cancellationToken);
}
