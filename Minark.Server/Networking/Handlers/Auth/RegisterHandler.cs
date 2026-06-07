using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Auth;

public sealed class RegisterHandler(IAuthService auth, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.RegisterRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<RegisterRequest>(payload);
        if (req is null)
        {
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.RegisterResponse,
            await auth.RegisterAsync(req));
    }
}