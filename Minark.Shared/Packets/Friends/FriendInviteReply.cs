namespace Minark.Shared.Packets.Friends;

/// <summary>
///     Réponse à une invitation d'ami (accepter ou refuser).
///     Le PacketType détermine l'action : FriendInviteAccept ou FriendInviteDecline.
///     Remplace : FriendInviteAccept, FriendInviteDecline.
/// </summary>
public class FriendInviteReply
{
    public string Token { get; init; } = string.Empty;
    public int FriendshipId { get; init; }
}