using LiteNetLib;

namespace Minark.GameServer.Network;

public class ConnectedPlayer
{
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string Token { get; init; } = string.Empty;
    public int PreviousStatusInt { get; init; } = 1;
    public GameUserStatus Status { get; set; } = GameUserStatus.InGame;
    public NetPeer Peer { get; init; } = null!;

    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Rot { get; set; }
    public long LastMoveTick { get; set; }

    public HashSet<int> FriendIds { get; init; } = [];
}