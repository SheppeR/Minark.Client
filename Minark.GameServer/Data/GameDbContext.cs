using Microsoft.EntityFrameworkCore;

namespace Minark.GameServer.Data;

// ── Entities (miroir léger des tables existantes) ─────────────────────────────
// ReSharper disable all EntityFramework.ModelValidation.UnlimitedStringLength
[Table("sessions")]
public class SessionEntity
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("user_id")]
    public int UserId { get; init; }

    [Column("token")]
    public string Token { get; init; } = string.Empty;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; init; }

    public UserEntity User { get; init; } = null!;
}

[Table("users")]
public class UserEntity
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("username")]
    public string Username { get; init; } = string.Empty;

    [Column("status")]
    public int Status { get; init; }

    [Column("avatar_url")]

    public string? AvatarUrl { get; init; }

    [Column("is_admin")]
    public bool IsAdmin { get; init; }
}

[Table("friendships")]
public class FriendshipEntity
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("requester_id")]
    public int RequesterId { get; init; }

    [Column("addressee_id")]
    public int AddresseeId { get; init; }

    [Column("status")]
    public FriendshipStatus Status { get; init; }

    public UserEntity Requester { get; init; } = null!;
    public UserEntity Addressee { get; init; } = null!;
}

public enum FriendshipStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Blocked = 3
}

// ── DbContext ─────────────────────────────────────────────────────────────────

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