namespace ModelFarm.Contracts.Auth;

/// <summary>
/// Request to register a new user.
/// </summary>
public sealed record RegisterRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

/// <summary>
/// Request to login.
/// </summary>
public sealed record LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

/// <summary>
/// Authentication response with tokens.
/// </summary>
public sealed record AuthResponse
{
    public required Guid UserId { get; init; }
    public required string Username { get; init; }
    public required string AccessToken { get; init; }
    public required DateTime AccessTokenExpires { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime RefreshTokenExpires { get; init; }
}

/// <summary>
/// Request to refresh tokens.
/// </summary>
public sealed record RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}

/// <summary>
/// User information.
/// </summary>
public sealed record UserInfo
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public DateTime? LastLoginAtUtc { get; init; }
}
