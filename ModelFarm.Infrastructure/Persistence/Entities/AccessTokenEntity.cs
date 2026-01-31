namespace ModelFarm.Infrastructure.Persistence.Entities;

/// <summary>
/// Refresh token entity for JWT authentication.
/// </summary>
public sealed class RefreshTokenEntity
{
    public Guid Id { get; set; }
    public required Guid UserId { get; set; }
    public required string Token { get; set; }
    public required DateTime ExpiresAtUtc { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByToken { get; set; }
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
    public bool IsRevoked => RevokedAtUtc != null;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation
    public UserEntity? User { get; set; }
}
