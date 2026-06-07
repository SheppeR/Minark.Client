using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Friends;

public sealed class FriendRequestHandler(
    IAuthService auth,
    IFriendService friends,
    ISessionStore sessionStore,
    IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.FriendRequestSend;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<FriendRequestSend>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            await sender.SendAsync(clientGuid, PacketType.FriendRequestResponse,
                new AckResponse { Success = false, ErrorMessage = "Token invalide." });
            return;
        }

        var (response, friendshipId) = await friends.SendFriendRequestAsync(session.UserId, req.TargetUsername);
        await sender.SendAsync(clientGuid, PacketType.FriendRequestResponse, response);

        if (response.Success)
        {
            var targetEntry = sessionStore.GetClientByUsername(req.TargetUsername);
            if (targetEntry.HasValue)
            {
                await sender.SendAsync(targetEntry.Value.ClientGuid, PacketType.FriendInviteReceived,
                    new FriendInviteReceived { FromUsername = session.User.Username, FriendshipId = friendshipId });
            }
        }
    }
}