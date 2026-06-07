using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Profile;

public sealed class UpdateAvatarHandler(IAuthService auth, IProfileService profile, IServerSender sender)
    : IPacketHandler
{
    public PacketType PacketType => PacketType.UpdateAvatarRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<UpdateAvatarRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            await sender.SendAsync(clientGuid, PacketType.UpdateAvatarResponse,
                new UpdateAvatarResponse { Success = false, ErrorMessage = "Token invalide." });
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.UpdateAvatarResponse,
            await profile.UpdateAvatarUrlAsync(session.UserId, req.AvatarUrl));
    }
}