using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Minark.Server.Data.Entities;

[Table("friendships")]
public class Friendship
{
    [Key]
    [Column("id")]
    public int Id { get; init; }

    [Column("requester_id")]
    public int RequesterId { get; init; }

    [Column("addressee_id")]
    public int AddresseeId { get; init; }

    [Column("status")]
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("RequesterId")]
    public User Requester { get; init; } = null!;

    [ForeignKey("AddresseeId")]
    public User Addressee { get; init; } = null!;
}