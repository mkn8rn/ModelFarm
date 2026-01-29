using ModelFarm.Application.Tasks;
using ModelFarm.Application.Tasks.Handlers;
using ModelFarm.Contracts.MarketData;
using ModelFarm.Contracts.Tasks;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for managing data ingestion operations.
/// Delegates actual execution to the background task manager.
/// </summary>
public sealed class IngestionService : IIngestionService
{
    private readonly IBackgroundTaskManager _taskManager;

    public IngestionService(IBackgroundTaskManager taskManager)
    {
        _taskManager = taskManager;
    }

    public Task<Guid> StartIngestionAsync(IngestionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var parameters = new DataIngestionParameters
        {
            Exchange = request.Exchange,
            Symbol = request.Symbol,
            Interval = request.Interval,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc
        };

        var task = _taskManager.ScheduleTask(
            BackgroundTaskType.DataIngestion,
            $"Ingest {request.Symbol} ({request.Interval.ToDisplayString()})",
            parameters);

        return Task.FromResult(task.Id);
    }

    public IngestionProgress GetProgress(Guid operationId)
    {
        var task = _taskManager.GetTask(operationId);
        
        if (task is null)
        {
            return new IngestionProgress
            {
                OperationId = operationId,
                Status = IngestionStatus.NotStarted,
                RecordsFetched = 0,
                EstimatedTotalRecords = 0,
                Message = "Not found"
            };
        }

        var status = task.Status switch
        {
            BackgroundTaskStatus.Pending => IngestionStatus.InProgress,
            BackgroundTaskStatus.Running => IngestionStatus.InProgress,
            BackgroundTaskStatus.Completed => IngestionStatus.Completed,
            BackgroundTaskStatus.Failed => IngestionStatus.Failed,
            BackgroundTaskStatus.Cancelled => IngestionStatus.Failed,
            _ => IngestionStatus.NotStarted
        };

        // Try to parse result for completed tasks
        IngestionResult? result = null;
        if (task.Status == BackgroundTaskStatus.Completed && task.ResultJson is not null)
        {
            try
            {
                var ingestionResult = TaskResult.FromJson<DataIngestionResult>(task.ResultJson);
                result = new IngestionResult
                {
                    Exchange = default,
                    Symbol = string.Empty,
                    Interval = default,
                    TotalRecords = ingestionResult.TotalRecords,
                    FirstTimestampUtc = ingestionResult.FirstTimestampUtc,
                    LastTimestampUtc = ingestionResult.LastTimestampUtc,
                    TimeSpanCovered = ingestionResult.LastTimestampUtc - ingestionResult.FirstTimestampUtc,
                    IngestionDuration = ingestionResult.Duration,
                    SampleRecords = []
                };
            }
            catch
            {
                // Ignore deserialization errors
            }
        }

        return new IngestionProgress
        {
            OperationId = operationId,
            Status = status,
            RecordsFetched = (int)task.ProgressCurrent,
            EstimatedTotalRecords = (int)task.ProgressTotal,
            Message = task.ProgressMessage ?? GetDefaultMessage(task.Status),
            StartedAtUtc = task.StartedAtUtc,
            CompletedAtUtc = task.CompletedAtUtc,
            ErrorMessage = task.ErrorMessage,
            Result = result
        };
    }

    public void CancelIngestion(Guid operationId)
    {
        _taskManager.CancelTask(operationId);
    }

    private static string GetDefaultMessage(BackgroundTaskStatus status) => status switch
    {
        BackgroundTaskStatus.Pending => "Queued...",
        BackgroundTaskStatus.Running => "Processing...",
        BackgroundTaskStatus.Completed => "Completed",
        BackgroundTaskStatus.Failed => "Failed",
        BackgroundTaskStatus.Cancelled => "Cancelled",
        _ => "Unknown"
    };

    private static void ValidateRequest(IngestionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            throw new ArgumentException("Symbol is required", nameof(request));

        if (request.StartTimeUtc >= request.EndTimeUtc)
            throw new ArgumentException("Start time must be before end time", nameof(request));

        if (request.EndTimeUtc > DateTime.UtcNow)
            throw new ArgumentException("End time cannot be in the future", nameof(request));
    }
}
