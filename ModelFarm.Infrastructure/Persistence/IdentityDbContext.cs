using Microsoft.EntityFrameworkCore;

namespace ModelFarm.Infrastructure.Persistence;

/// <summary>
/// DbContext for identity and authentication data.
/// </summary>
public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");
    }
}
