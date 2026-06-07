using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Profile;

public sealed class ChangePasswordHandler(IAuthService auth, IProfileService profile, IServerSender sender)
    : IPacketHandler
{
    public PacketType PacketType => PacketType.ChangePasswordRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<ChangePasswordRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            await sender.SendAsync(clientGuid, PacketType.ChangePasswordResponse,
                new AckResponse { Success = false, ErrorMessage = "Token invalide." });
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.ChangePasswordResponse,
            await profile.ChangePasswordAsync(session.UserId, req.OldPasswordHash, req.NewPasswordHash));
    }
}