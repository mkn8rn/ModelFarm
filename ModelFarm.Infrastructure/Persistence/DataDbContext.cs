using Microsoft.EntityFrameworkCore;
using ModelFarm.Infrastructure.Persistence.Entities;

namespace ModelFarm.Infrastructure.Persistence;

/// <summary>
/// DbContext for market data storage (klines, trades, datasets).
/// </summary>
public sealed class DataDbContext : DbContext
{
    public DataDbContext(DbContextOptions<DataDbContext> options) : base(options) { }

    public DbSet<KlineEntity> Klines => Set<KlineEntity>();
    public DbSet<DatasetEntity> Datasets => Set<DatasetEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("data");

        modelBuilder.Entity<KlineEntity>(entity =>
        {
            entity.Property(e => e.Symbol).HasMaxLength(20);
            entity.Property(e => e.Open).HasPrecision(28, 8);
            entity.Property(e => e.High).HasPrecision(28, 8);
            entity.Property(e => e.Low).HasPrecision(28, 8);
            entity.Property(e => e.Close).HasPrecision(28, 8);
            entity.Property(e => e.Volume).HasPrecision(28, 8);
            entity.Property(e => e.QuoteAssetVolume).HasPrecision(28, 8);
        });

        modelBuilder.Entity<DatasetEntity>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Symbol).HasMaxLength(20);
        });
    }
}
