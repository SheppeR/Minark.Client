namespace Minark.Shared.Packets.Friends;

/// <summary>Poussé par le serveur aux deux parties après un ajout ou suppression d'ami.</summary>
public class FriendListChanged
{
    public string Reason { get; init; } = string.Empty; // "added" | "removed"
    public string OtherUsername { get; init; } = string.Empty;
}