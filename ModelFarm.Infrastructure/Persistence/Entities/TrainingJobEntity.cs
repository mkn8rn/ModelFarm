using ModelFarm.Contracts.Training;
using System.Text.Json;

namespace ModelFarm.Infrastructure.Persistence.Entities;

public sealed class TrainingJobEntity
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required Guid ConfigurationId { get; set; }
    public required TrainingJobStatus Status { get; set; }

    // Progress tracking
    public int CurrentEpoch { get; set; }
    public int TotalEpochs { get; set; }
    public double? TrainingLoss { get; set; }
    public double? ValidationLoss { get; set; }
    public double? BestValidationLoss { get; set; }
    public int EpochsSinceImprovement { get; set; }

    // Retry tracking
    public int CurrentAttempt { get; set; } = 1;
    public int MaxAttempts { get; set; } = 1;

    // Status flags
    public bool IsPaused { get; set; } = false;
    public required string Message { get; set; }
    public string? ErrorMessage { get; set; }

    // Timestamps
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    // Result (stored as JSON)
    public string? ResultJson { get; set; }

    // Checkpoint tracking
    public bool HasCheckpoint { get; set; } = false;
    public DateTime? LastCheckpointAtUtc { get; set; }

    // Accumulated training duration (for resume)
    public long AccumulatedTrainingTicks { get; set; } = 0;

    public TrainingJob ToJob() => new()
    {
        Id = Id,
        Name = Name,
        ConfigurationId = ConfigurationId,
        Status = Status,
        CurrentEpoch = CurrentEpoch,
        TotalEpochs = TotalEpochs,
        TrainingLoss = TrainingLoss,
        ValidationLoss = ValidationLoss,
        BestValidationLoss = BestValidationLoss,
        EpochsSinceImprovement = EpochsSinceImprovement,
        CurrentAttempt = CurrentAttempt,
        MaxAttempts = MaxAttempts,
        IsPaused = IsPaused,
        Message = Message,
        ErrorMessage = ErrorMessage,
        CreatedAtUtc = CreatedAtUtc,
        StartedAtUtc = StartedAtUtc,
        CompletedAtUtc = CompletedAtUtc,
        Result = ResultJson != null ? JsonSerializer.Deserialize<TrainingResult>(ResultJson) : null,
        HasCheckpoint = HasCheckpoint
    };

    public static TrainingJobEntity FromJob(TrainingJob job) => new()
    {
        Id = job.Id,
        Name = job.Name,
        ConfigurationId = job.ConfigurationId,
        Status = job.Status,
        CurrentEpoch = job.CurrentEpoch,
        TotalEpochs = job.TotalEpochs,
        TrainingLoss = job.TrainingLoss,
        ValidationLoss = job.ValidationLoss,
        BestValidationLoss = job.BestValidationLoss,
        EpochsSinceImprovement = job.EpochsSinceImprovement,
        CurrentAttempt = job.CurrentAttempt,
        MaxAttempts = job.MaxAttempts,
        IsPaused = job.IsPaused,
        Message = job.Message,
        ErrorMessage = job.ErrorMessage,
        CreatedAtUtc = job.CreatedAtUtc,
        StartedAtUtc = job.StartedAtUtc,
        CompletedAtUtc = job.CompletedAtUtc,
        ResultJson = job.Result != null ? JsonSerializer.Serialize(job.Result) : null
    };

    public void UpdateFrom(TrainingJob job)
    {
        Status = job.Status;
        CurrentEpoch = job.CurrentEpoch;
        TotalEpochs = job.TotalEpochs;
        TrainingLoss = job.TrainingLoss;
        ValidationLoss = job.ValidationLoss;
        BestValidationLoss = job.BestValidationLoss;
        EpochsSinceImprovement = job.EpochsSinceImprovement;
        CurrentAttempt = job.CurrentAttempt;
        MaxAttempts = job.MaxAttempts;
        IsPaused = job.IsPaused;
        Message = job.Message;
        ErrorMessage = job.ErrorMessage;
        StartedAtUtc = job.StartedAtUtc;
        CompletedAtUtc = job.CompletedAtUtc;
        ResultJson = job.Result != null ? JsonSerializer.Serialize(job.Result) : null;
    }
}
