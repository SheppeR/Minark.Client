using LiteNetLib;

namespace Minark.GameServer.Handlers;

public class PacketDispatcher
{
    private readonly Dictionary<GamePacketType, IPacketHandler> _handlers;
    private readonly ILogger<PacketDispatcher> _log;

    public PacketDispatcher(IEnumerable<IPacketHandler> handlers, ILogger<PacketDispatcher> log)
    {
        _log = log;
        _handlers = handlers.ToDictionary(h => h.PacketType);
    }

    public async Task DispatchAsync(NetPeer peer, byte[] data, int offset, int length, CancellationToken ct)
    {
        var packet = GamePacketSerializer.Deserialize(data, offset, length);
        if (packet is null)
        {
            _log.LogWarning("Paquet invalide reçu de {PeerId}", peer.Id);
            return;
        }

        if (!_handlers.TryGetValue(packet.Type, out var handler))
        {
            _log.LogWarning("Aucun handler pour {Type}", packet.Type);
            return;
        }

        try
        {
            await handler.HandleAsync(peer, packet, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erreur handler {Type}", packet.Type);
        }
    }
}