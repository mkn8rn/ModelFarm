using ModelFarm.Contracts.Tasks;

namespace ModelFarm.Infrastructure.Persistence.Entities;

/// <summary>
/// Database entity for background tasks.
/// </summary>
public sealed class BackgroundTaskEntity
{
    public Guid Id { get; set; }
    public BackgroundTaskType Type { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public BackgroundTaskStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int ProgressPercent { get; set; }
    public string? ProgressMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public long ProgressCurrent { get; set; }
    public long ProgressTotal { get; set; }
    public required string ParametersJson { get; set; }
    public string? ResultJson { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public int Priority { get; set; }

    public BackgroundTask ToTask() => new()
    {
        Id = Id,
        Type = Type,
        Name = Name,
        Description = Description,
        Status = Status,
        CreatedAtUtc = CreatedAtUtc,
        StartedAtUtc = StartedAtUtc,
        CompletedAtUtc = CompletedAtUtc,
        ProgressPercent = ProgressPercent,
        ProgressMessage = ProgressMessage,
        ErrorMessage = ErrorMessage,
        ProgressCurrent = ProgressCurrent,
        ProgressTotal = ProgressTotal,
        ParametersJson = ParametersJson,
        ResultJson = ResultJson,
        RelatedEntityId = RelatedEntityId,
        Priority = Priority
    };

    public static BackgroundTaskEntity FromTask(BackgroundTask task) => new()
    {
        Id = task.Id,
        Type = task.Type,
        Name = task.Name,
        Description = task.Description,
        Status = task.Status,
        CreatedAtUtc = task.CreatedAtUtc,
        StartedAtUtc = task.StartedAtUtc,
        CompletedAtUtc = task.CompletedAtUtc,
        ProgressPercent = task.ProgressPercent,
        ProgressMessage = task.ProgressMessage,
        ErrorMessage = task.ErrorMessage,
        ProgressCurrent = task.ProgressCurrent,
        ProgressTotal = task.ProgressTotal,
        ParametersJson = task.ParametersJson,
        ResultJson = task.ResultJson,
        RelatedEntityId = task.RelatedEntityId,
        Priority = task.Priority
    };

    public void UpdateFrom(BackgroundTask task)
    {
        Status = task.Status;
        StartedAtUtc = task.StartedAtUtc;
        CompletedAtUtc = task.CompletedAtUtc;
        ProgressPercent = task.ProgressPercent;
        ProgressMessage = task.ProgressMessage;
        ErrorMessage = task.ErrorMessage;
        ProgressCurrent = task.ProgressCurrent;
        ProgressTotal = task.ProgressTotal;
        ResultJson = task.ResultJson;
    }
}
