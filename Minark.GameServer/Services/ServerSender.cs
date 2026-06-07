using LiteNetLib;
using LiteNetLib.Utils;

namespace Minark.GameServer.Services;

public interface IServerSender
{
    void Send<T>(NetPeer peer, GamePacketType type, T payload,
        DeliveryMethod delivery = DeliveryMethod.ReliableOrdered);

    void Broadcast<T>(IEnumerable<NetPeer> peers, GamePacketType type, T payload,
        DeliveryMethod delivery = DeliveryMethod.ReliableOrdered);
}

public class ServerSender : IServerSender
{
    public void Send<T>(NetPeer peer, GamePacketType type, T payload,
        DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
    {
        var writer = new NetDataWriter();
        writer.Put(GamePacketSerializer.Serialize(type, payload));
        peer.Send(writer, delivery);
    }

    public void Broadcast<T>(IEnumerable<NetPeer> peers, GamePacketType type, T payload,
        DeliveryMethod delivery = DeliveryMethod.ReliableOrdered)
    {
        // Serialize once, reuse the same writer (reset between sends)
        var data = GamePacketSerializer.Serialize(type, payload);
        var writer = new NetDataWriter();
        foreach (var peer in peers)
        {
            writer.Reset();
            writer.Put(data);
            peer.Send(writer, delivery);
        }
    }
}