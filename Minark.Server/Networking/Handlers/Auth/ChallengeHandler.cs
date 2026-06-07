using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Auth;

public sealed class ChallengeHandler(IAuthService auth, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.ChallengeRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<ChallengeRequest>(payload);
        if (req is null || string.IsNullOrWhiteSpace(req.Username))
        {
            await sender.SendAsync(clientGuid, PacketType.ChallengeResponse,
                new ChallengeResponse { Nonce = string.Empty });
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.ChallengeResponse,
            await auth.GetChallengeAsync(req.Username));
    }
}