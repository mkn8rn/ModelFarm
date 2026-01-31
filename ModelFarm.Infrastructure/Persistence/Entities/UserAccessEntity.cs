namespace ModelFarm.Infrastructure.Persistence.Entities;

/// <summary>
/// Tracks user access/activity for audit purposes.
/// Stored in ApplicationDbContext to link users with their actions.
/// </summary>
public sealed class UserAccessEntity
{
    public Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required string Action { get; set; }
    public string? ResourceType { get; set; }
    public Guid? ResourceId { get; set; }
    public string? IpAddress { get; set; }
    public required DateTime AccessedAtUtc { get; set; }
}
