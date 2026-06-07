using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Profile;

public sealed class BlockHandler(IAuthService auth, IProfileService profile, IServerSender sender, bool block)
    : IPacketHandler
{
    public PacketType PacketType => block ? PacketType.BlockUser : PacketType.UnblockUser;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<BlockUserRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            await sender.SendAsync(clientGuid, PacketType.BlockResponse, new AckResponse { Success = false });
            return;
        }

        var resp = block
            ? await profile.BlockUserAsync(session.UserId, req.TargetUsername)
            : await profile.UnblockUserAsync(session.UserId, req.TargetUsername);
        await sender.SendAsync(clientGuid, PacketType.BlockResponse, resp);
    }
}