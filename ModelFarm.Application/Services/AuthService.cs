using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ModelFarm.Contracts.Auth;
using ModelFarm.Infrastructure.Persistence;
using ModelFarm.Infrastructure.Persistence.Entities;

namespace ModelFarm.Application.Services;

/// <summary>
/// JWT-based authentication service.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IDbContextFactory<IdentityDbContext> _dbContextFactory;
    private readonly JwtSettings _jwtSettings;

    public AuthService(IDbContextFactory<IdentityDbContext> dbContextFactory, JwtSettings jwtSettings)
    {
        _dbContextFactory = dbContextFactory;
        _jwtSettings = jwtSettings;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Check if username already exists
        var existingUser = await db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (existingUser != null)
        {
            throw new InvalidOperationException("Username already exists");
        }

        // Create user
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = HashPassword(request.Password),
            CreatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        // Generate tokens
        return await GenerateAuthResponseAsync(db, user, cancellationToken);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive, cancellationToken);

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            return null;



        // Update last login
        user.LastLoginAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return await GenerateAuthResponseAsync(db, user, cancellationToken);
    }

    public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var token = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);

        if (token == null || !token.IsActive || token.User == null || !token.User.IsActive)
            return null;

        // Revoke old token
        token.RevokedAtUtc = DateTime.UtcNow;

        // Generate new tokens
        var response = await GenerateAuthResponseAsync(db, token.User, cancellationToken);
        
        token.ReplacedByToken = response.RefreshToken;
        await db.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var token = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken, cancellationToken);


        if (token == null || !token.IsActive)
            return false;

        token.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserInfo?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user == null || !user.IsActive)
            return null;

        return new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            CreatedAtUtc = user.CreatedAtUtc,
            LastLoginAtUtc = user.LastLoginAtUtc
        };
    }


    public Guid? ValidateAccessToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;
            var userId = Guid.Parse(jwtToken.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
            return userId;
        }
        catch
        {
            return null;
        }
    }

    private async Task<AuthResponse> GenerateAuthResponseAsync(IdentityDbContext db, UserEntity user, CancellationToken cancellationToken)
    {
        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken();

        var refreshTokenEntity = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedAtUtc = DateTime.UtcNow
        };

        db.RefreshTokens.Add(refreshTokenEntity);
        await db.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            UserId = user.Id,
            Username = user.Username,
            AccessToken = accessToken,
            AccessTokenExpires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            RefreshToken = refreshToken,
            RefreshTokenExpires = refreshTokenEntity.ExpiresAtUtc
        };
    }

    private string GenerateAccessToken(UserEntity user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}

/// <summary>
/// JWT configuration settings.
/// </summary>
public sealed class JwtSettings
{
    public required string Secret { get; init; }
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public int AccessTokenExpirationMinutes { get; init; } = 15;
    public int RefreshTokenExpirationDays { get; init; } = 7;
}
