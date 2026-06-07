namespace Minark.Shared.Packets.Friends;

/// <summary>Envoyé par le client pour refuser une invitation.</summary>
public class FriendInviteDecline
{
    public string Token { get; set; } = string.Empty;
    public int FriendshipId { get; set; }
}