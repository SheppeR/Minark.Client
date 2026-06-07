using LiteNetLib;
using Minark.GameServer.Network;

namespace Minark.GameServer.Handlers;

public class PlayerMoveHandler(PlayerRegistry registry) : IPacketHandler
{
    public GamePacketType PacketType => GamePacketType.PlayerMove;

    public Task HandleAsync(NetPeer peer, GamePacket packet, CancellationToken ct)
    {
        if (!registry.TryGetByPeer(peer.Id, out var player) || player is null)
        {
            return Task.CompletedTask;
        }

        var move = GamePacketSerializer.DeserializePayload<PlayerMovePacket>(packet.Payload);
        if (move is null)
        {
            return Task.CompletedTask;
        }

        player.X = move.X;
        player.Y = move.Y;
        player.Z = move.Z;
        player.Rot = move.Rot;
        player.LastMoveTick = Environment.TickCount64;

        return Task.CompletedTask;
    }
}