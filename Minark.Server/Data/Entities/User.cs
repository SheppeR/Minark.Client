using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength

namespace Minark.Server.Data.Entities;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("username")]
    [MaxLength(50)]
    public string Username { get; init; } = string.Empty;

    [Column("email")]
    [MaxLength(200)]
    public string Email { get; init; } = string.Empty;

    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("status")]
    public int Status { get; set; }

    [Column("avatar_url")]
    [MaxLength(500)]
    public string? AvatarUrl { get; init; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [Column("last_seen_at")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [Column("is_admin")]
    public bool IsAdmin { get; init; }

    public ICollection<Friendship> FriendshipsInitiated { get; init; } = new List<Friendship>();
    public ICollection<Friendship> FriendshipsReceived { get; init; } = new List<Friendship>();
    public ICollection<Session> Sessions { get; init; } = new List<Session>();
}