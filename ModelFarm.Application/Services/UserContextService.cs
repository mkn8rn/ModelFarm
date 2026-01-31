using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ModelFarm.Infrastructure.Persistence;
using ModelFarm.Infrastructure.Persistence.Entities;

namespace ModelFarm.Application.Services;

/// <summary>
/// Service for accessing current user context and managing user ownership.
/// </summary>
public interface IUserContextService
{
    /// <summary>
    /// Gets the current authenticated user's ID, or null if not authenticated.
    /// </summary>
    Guid? GetCurrentUserId();

    /// <summary>
    /// Gets the current authenticated user's ID, throws if not authenticated.
    /// </summary>
    Guid GetRequiredUserId();

    /// <summary>
    /// Creates an ownership record linking a resource to the current user.
    /// </summary>
    Task CreateOwnershipAsync(string resourceType, Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the current user owns a resource.
    /// </summary>
    Task<bool> OwnsResourceAsync(string resourceType, Guid resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all resource IDs of a type owned by the current user.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetOwnedResourceIdsAsync(string resourceType, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of user context service using HTTP context.
/// </summary>
public sealed class UserContextService : IUserContextService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

    public UserContextService(
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContextFactory = dbContextFactory;
    }

    public Guid? GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    public Guid GetRequiredUserId()
    {
        return GetCurrentUserId() 
            ?? throw new UnauthorizedAccessException("User is not authenticated");
    }

    public async Task CreateOwnershipAsync(string resourceType, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return; // Anonymous user, no ownership tracking

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var ownership = new UserOwnershipEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId.Value,
            ResourceType = resourceType,
            ResourceId = resourceId,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.UserOwnerships.Add(ownership);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> OwnsResourceAsync(string resourceType, Guid resourceId, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return false;

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.UserOwnerships.AnyAsync(
            o => o.UserId == userId.Value && o.ResourceType == resourceType && o.ResourceId == resourceId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetOwnedResourceIdsAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return [];

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await db.UserOwnerships
            .Where(o => o.UserId == userId.Value && o.ResourceType == resourceType)
            .Select(o => o.ResourceId)
            .ToListAsync(cancellationToken);
    }
}
