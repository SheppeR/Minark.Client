using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Auth;

public sealed class LogoutHandler(
    IAuthService auth,
    ISessionStore sessionStore,
    IServerSender sender,
    IFriendService friends) : IPacketHandler
{
    public PacketType PacketType => PacketType.LogoutRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<TokenRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = sessionStore.GetUser(clientGuid);
        if (session is not null)
        {
            await friends.UpdateStatusAsync(session.Value.UserId, UserStatus.Offline);
            await sender.PushStatusToFriendsAsync(
                session.Value.UserId, session.Value.Username, UserStatus.Offline);
        }

        var resp = await auth.LogoutAsync(req.Token);
        sessionStore.RemoveClient(clientGuid);
        await sender.SendAsync(clientGuid, PacketType.LogoutResponse, resp);
    }
}