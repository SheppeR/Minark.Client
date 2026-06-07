namespace Minark.Shared.Packets.Friends;

/// <summary>Envoyé par le client pour accepter une invitation.</summary>
public class FriendInviteAccept
{
    public string Token { get; set; } = string.Empty;
    public int FriendshipId { get; set; }
}