namespace Minark.Server.Networking.Handlers;

public sealed class PingHandler(IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.Ping;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        await sender.SendAsync(clientGuid, PacketType.Pong, new { });
    }
}