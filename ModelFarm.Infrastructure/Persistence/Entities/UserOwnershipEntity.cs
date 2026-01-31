namespace ModelFarm.Infrastructure.Persistence.Entities;

/// <summary>
/// Tracks ownership of resources by users.
/// Links users to their created training jobs, datasets, tests, configurations, etc.
/// </summary>
public sealed class UserOwnershipEntity
{
    public Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required string ResourceType { get; set; }
    public required Guid ResourceId { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// Resource types for ownership tracking.
/// </summary>
public static class ResourceTypes
{
    public const string Dataset = "Dataset";
    public const string TrainingConfiguration = "TrainingConfiguration";
    public const string TrainingJob = "TrainingJob";
    public const string ModelTest = "ModelTest";
}
