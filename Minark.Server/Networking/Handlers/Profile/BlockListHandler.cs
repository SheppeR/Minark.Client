using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Profile;

public sealed class BlockListHandler(IAuthService auth, IProfileService profile, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.BlockListRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<TokenRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            await sender.SendAsync(clientGuid, PacketType.BlockListResponse, new BlockListResponse { Success = false });
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.BlockListResponse,
            await profile.GetBlockListAsync(session.UserId));
    }
}