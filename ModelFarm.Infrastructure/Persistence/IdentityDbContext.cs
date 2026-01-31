using Microsoft.EntityFrameworkCore;
using ModelFarm.Infrastructure.Persistence.Entities;

namespace ModelFarm.Infrastructure.Persistence;

/// <summary>
/// DbContext for identity and authentication data.
/// </summary>
public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);

            entity.HasIndex(e => e.Username).IsUnique();
        });

        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).HasMaxLength(256);
            entity.Property(e => e.ReplacedByToken).HasMaxLength(256);

            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
