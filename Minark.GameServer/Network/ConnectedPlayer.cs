using LiteNetLib;

namespace Minark.GameServer.Network;

/// <summary>
///     Représente un joueur authentifié et connecté au GameServer.
///     Thread-safe en lecture (les champs Position sont mis à jour par le tick loop).
/// </summary>
public class ConnectedPlayer
{
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public GameUserStatus Status { get; set; } = GameUserStatus.InGame;
    public NetPeer Peer { get; init; } = null!;

    // Position — mise à jour par PlayerMovePacket (Unreliable)
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Rot { get; set; }
    public long LastMoveTick { get; set; }

    // Liste des friend IDs (chargée à l'auth, pour les push FriendStatusUpdate)
    public HashSet<int> FriendIds { get; init; } = [];
}