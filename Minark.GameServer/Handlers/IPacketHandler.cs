using LiteNetLib;

namespace Minark.GameServer.Handlers;

public interface IPacketHandler
{
    GamePacketType PacketType { get; }
    Task HandleAsync(NetPeer peer, GamePacket packet, CancellationToken ct);
}