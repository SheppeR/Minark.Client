namespace Minark.Shared.Packets.Friends;

/// <summary>Poussé par le serveur à la cible quand quelqu'un envoie une invitation.</summary>
public class FriendInviteReceived
{
    public string FromUsername { get; init; } = string.Empty;
    public int FriendshipId { get; init; }
}