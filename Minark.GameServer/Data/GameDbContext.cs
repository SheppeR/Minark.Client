using Microsoft.EntityFrameworkCore;

namespace Minark.GameServer.Data;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<FriendshipEntity> Friendships => Set<FriendshipEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SessionEntity>(e =>
        {
            e.HasIndex(s => s.Token).IsUnique();
            e.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId);
        });

        modelBuilder.Entity<FriendshipEntity>(e =>
        {
            e.HasOne(f => f.Requester).WithMany().HasForeignKey(f => f.RequesterId);
            e.HasOne(f => f.Addressee).WithMany().HasForeignKey(f => f.AddresseeId);
        });
    }
}