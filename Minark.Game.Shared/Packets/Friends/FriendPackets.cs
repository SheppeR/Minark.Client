namespace Minark.Game.Shared.Packets.Friends;

public class FriendListRequest
{
    // vide — le serveur identifie le joueur via sa connexion authentifiée
}

public class FriendListResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public GameFriendDto[] Friends { get; set; } = [];
}

/// <summary>Push serveur → client quand un ami change de statut en jeu.</summary>
public class FriendStatusUpdate
{
    public int FriendId { get; set; }
    public GameUserStatus Status { get; set; }
}