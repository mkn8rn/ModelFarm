using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelFarm.Application.Tasks;
using ModelFarm.Application.Tasks.Handlers;
using ModelFarm.Contracts.Tasks;
using ModelFarm.Contracts.Training;
using ModelFarm.Infrastructure.Persistence;

namespace ModelFarm.Application.Services;

/// <summary>
/// Background service that synchronizes dataset statuses with their associated tasks on startup.
/// For datasets without valid tasks, creates new recovery tasks.
/// </summary>
public sealed class OperationRecoveryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundTaskManager _taskManager;
    private readonly ILogger<OperationRecoveryService> _logger;

    public OperationRecoveryService(
        IServiceScopeFactory scopeFactory,
        IBackgroundTaskManager taskManager,
        ILogger<OperationRecoveryService> logger)
    {
        _scopeFactory = scopeFactory;
        _taskManager = taskManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Delay to allow task manager to initialize from database first
        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

        _logger.LogInformation("Dataset recovery service starting...");

        try
        {
            await SyncDatasetsWithTasksAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Dataset recovery service cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during dataset recovery");
        }

        _logger.LogInformation("Dataset recovery service completed");
    }

    private async Task SyncDatasetsWithTasksAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DataDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Find datasets that are in progress but may need task recovery
        var incompleteDatasets = await db.Datasets
            .Where(d => d.Status == DatasetStatus.Downloading || d.Status == DatasetStatus.Pending)
            .ToListAsync(cancellationToken);

        if (incompleteDatasets.Count == 0)
        {
            _logger.LogInformation("No incomplete datasets to synchronize");
            return;
        }

        _logger.LogInformation("Found {Count} incomplete dataset(s) to synchronize", incompleteDatasets.Count);

        foreach (var dataset in incompleteDatasets)
        {
            try
            {
                // Check if there's already a task for this dataset
                BackgroundTask? existingTask = null;
                if (dataset.IngestionOperationId.HasValue)
                {
                    existingTask = _taskManager.GetTask(dataset.IngestionOperationId.Value);
                }

                if (existingTask is not null && existingTask.Status is BackgroundTaskStatus.Pending or BackgroundTaskStatus.Running)
                {
                    _logger.LogInformation(
                        "Dataset {DatasetId} already has active task {TaskId}",
                        dataset.Id, existingTask.Id);
                    continue;
                }

                // Need to create a new recovery task
                _logger.LogInformation(
                    "Creating recovery task for dataset {DatasetId} ({Name})",
                    dataset.Id, dataset.Name);

                var parameters = new DataIngestionParameters
                {
                    Exchange = dataset.Exchange,
                    Symbol = dataset.Symbol,
                    Interval = dataset.Interval,
                    StartTimeUtc = dataset.StartTimeUtc,
                    EndTimeUtc = dataset.EndTimeUtc
                };

                var task = _taskManager.ScheduleTask(
                    BackgroundTaskType.DataIngestion,
                    $"Recovery: {dataset.Name}",
                    parameters,
                    relatedEntityId: dataset.Id,
                    priority: 50);

                dataset.IngestionOperationId = task.Id;
                dataset.Status = DatasetStatus.Downloading;
                dataset.UpdatedAtUtc = DateTime.UtcNow;

                await db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Created recovery task {TaskId} for dataset {DatasetId}",
                    task.Id, dataset.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover dataset {DatasetId}", dataset.Id);

                dataset.Status = DatasetStatus.Failed;
                dataset.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
