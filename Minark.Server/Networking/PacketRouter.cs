namespace Minark.Server.Networking;

/// <summary>
///     Route les paquets entrants vers le bon IPacketHandler.
///     Crée un scope DI par paquet pour que les handlers accèdent aux services Scoped (DbContext etc).
/// </summary>
public sealed class PacketRouter(
    IServiceScopeFactory scopeFactory,
    ILogger<PacketRouter> logger)
{
    public async Task HandleAsync(Guid clientGuid, byte[] data)
    {
        var packet = PacketSerializer.Deserialize(data);
        if (packet is null)
        {
            logger.LogWarning("Paquet illisible depuis {Client}", clientGuid);
            return;
        }

        logger.LogDebug("Received {PacketType} from {Client}", packet.Type, clientGuid);

        await using var scope = scopeFactory.CreateAsyncScope();
        var handlers = scope.ServiceProvider.GetServices<IPacketHandler>();
        var handler = handlers.FirstOrDefault(h => h.PacketType == packet.Type);

        if (handler is null)
        {
            logger.LogWarning("Aucun handler pour {PacketType}", packet.Type);
            return;
        }

        try
        {
            await handler.HandleAsync(clientGuid, packet.Payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erreur handler {PacketType} depuis {Client}", packet.Type, clientGuid);
        }
    }
}