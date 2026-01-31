using ModelFarm.Contracts.Auth;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for user authentication and authorization.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new user.
    /// </summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user and returns tokens.
    /// </summary>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a refresh token.
    /// </summary>
    Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets user information by ID.
    /// </summary>
    Task<UserInfo?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an access token and returns the user ID if valid.
    /// </summary>
    Guid? ValidateAccessToken(string token);
}
