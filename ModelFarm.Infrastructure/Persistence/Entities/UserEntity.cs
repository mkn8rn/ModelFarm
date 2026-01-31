namespace ModelFarm.Infrastructure.Persistence.Entities;

/// <summary>
/// User account entity for authentication.
/// </summary>
public sealed class UserEntity
{
    public Guid Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
