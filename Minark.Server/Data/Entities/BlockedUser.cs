using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minark.Server.Data.Entities;

[Table("blocked_users")]
public class BlockedUser
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("blocker_id")]
    public int BlockerId { get; init; }

    [Column("blocked_id")]
    public int BlockedId { get; init; }

    [Column("created_at")]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [ForeignKey("BlockerId")]
    public User Blocker { get; init; } = null!;

    [ForeignKey("BlockedId")]
    public User Blocked { get; init; } = null!;
}