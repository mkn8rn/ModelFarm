using Microsoft.EntityFrameworkCore;
using ModelFarm.Infrastructure.Persistence.Entities;

namespace ModelFarm.Infrastructure.Persistence;

/// <summary>
/// DbContext for general application data (tasks, settings, metadata).
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<BackgroundTaskEntity> BackgroundTasks => Set<BackgroundTaskEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("app");

        modelBuilder.Entity<BackgroundTaskEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.ProgressMessage).HasMaxLength(500);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

            // Indexes for common queries
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.RelatedEntityId);
            entity.HasIndex(e => new { e.Status, e.Priority, e.CreatedAtUtc });
        });
    }
}
