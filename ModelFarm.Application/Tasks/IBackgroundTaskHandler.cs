using System.Text.Json;
using ModelFarm.Contracts.Tasks;

namespace ModelFarm.Application.Tasks;

/// <summary>
/// Interface for background task handlers. Each task type has a corresponding handler.
/// </summary>
public interface IBackgroundTaskHandler
{
    BackgroundTaskType TaskType { get; }
    
    Task ExecuteAsync(BackgroundTask task, IProgress<TaskProgressUpdate> progress, CancellationToken cancellationToken);
}

/// <summary>
/// Progress update for a background task.
/// </summary>
public sealed record TaskProgressUpdate
{
    public int ProgressPercent { get; init; }
    public string? Message { get; init; }
    public long Current { get; init; }
    public long Total { get; init; }
}

/// <summary>
/// Base class for task parameters with JSON serialization helpers.
/// </summary>
public abstract record TaskParameters
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string ToJson() => JsonSerializer.Serialize(this, GetType(), JsonOptions);
    
    public static T FromJson<T>(string json) where T : TaskParameters
        => JsonSerializer.Deserialize<T>(json, JsonOptions) 
           ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");
}

/// <summary>
/// Base class for task results with JSON serialization helpers.
/// </summary>
public abstract record TaskResult
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string ToJson() => JsonSerializer.Serialize(this, GetType(), JsonOptions);
    
    public static T FromJson<T>(string json) where T : TaskResult
        => JsonSerializer.Deserialize<T>(json, JsonOptions) 
           ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}");
}
