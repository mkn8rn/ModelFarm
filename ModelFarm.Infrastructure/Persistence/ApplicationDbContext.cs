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
    public DbSet<TrainingConfigurationEntity> TrainingConfigurations => Set<TrainingConfigurationEntity>();
    public DbSet<TrainingJobEntity> TrainingJobs => Set<TrainingJobEntity>();
    public DbSet<ModelTestEntity> ModelTests => Set<ModelTestEntity>();
    public DbSet<UserAccessEntity> UserAccess => Set<UserAccessEntity>();

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

        modelBuilder.Entity<TrainingConfigurationEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.HiddenLayerSizesJson).HasMaxLength(500);
            entity.Property(e => e.PerformanceRequirementsJson).HasMaxLength(2000);
            entity.Property(e => e.TradingEnvironmentJson).HasMaxLength(2000);

            // Indexes for common queries 
            entity.HasIndex(e => e.DatasetId);
            entity.HasIndex(e => e.CreatedAtUtc);
        });

        modelBuilder.Entity<TrainingJobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

            // Indexes for common queries
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ConfigurationId);
            entity.HasIndex(e => e.CreatedAtUtc);
            entity.HasIndex(e => new { e.Status, e.CreatedAtUtc });
        });

        modelBuilder.Entity<ModelTestEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.ModelName).HasMaxLength(200);
            entity.Property(e => e.ModelType).HasMaxLength(50);
            entity.Property(e => e.DatasetName).HasMaxLength(200);
            entity.Property(e => e.Symbol).HasMaxLength(50);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

            // Indexes for common queries
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ModelJobId);
            entity.HasIndex(e => e.DatasetId);
            entity.HasIndex(e => e.CreatedAtUtc);
        });

        modelBuilder.Entity<UserAccessEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.ResourceType).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AccessedAtUtc);
        });
    }
}
