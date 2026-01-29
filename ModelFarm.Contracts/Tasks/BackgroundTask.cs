namespace ModelFarm.Contracts.Tasks;

/// <summary>
/// Represents a background task that can be scheduled and executed.
/// </summary>
public sealed record BackgroundTask
{
    public required Guid Id { get; init; }
    public required BackgroundTaskType Type { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required BackgroundTaskStatus Status { get; set; }
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int ProgressPercent { get; set; }
    public string? ProgressMessage { get; set; }
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Current progress count (e.g., records processed).
    /// </summary>
    public long ProgressCurrent { get; set; }
    
    /// <summary>
    /// Estimated total for progress (e.g., total records expected).
    /// </summary>
    public long ProgressTotal { get; set; }
    
    /// <summary>
    /// Serialized task parameters (JSON).
    /// </summary>
    public required string ParametersJson { get; init; }
    
    /// <summary>
    /// Serialized task result (JSON), available after completion.
    /// </summary>
    public string? ResultJson { get; set; }
    
    /// <summary>
    /// Optional reference to a related entity (e.g., DatasetId).
    /// </summary>
    public Guid? RelatedEntityId { get; init; }
    
    /// <summary>
    /// Priority for task execution (lower = higher priority).
    /// </summary>
    public int Priority { get; init; } = 100;
}

/// <summary>
/// Types of background tasks that can be scheduled.
/// </summary>
public enum BackgroundTaskType
{
    DataIngestion,
    ModelTraining,
    DataExport,
    DataValidation
}

/// <summary>
/// Status of a background task.
/// </summary>
public enum BackgroundTaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
