namespace Minark.GameServer.Data;

// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength
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