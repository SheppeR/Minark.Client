using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Friends;

public sealed class FriendAcceptHandler(
    IAuthService auth,
    IFriendService friends,
    IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.FriendInviteAccept;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<FriendInviteReply>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        var (ok, requesterUsername) = await friends.AcceptFriendRequestAsync(session.UserId, req.FriendshipId);
        if (ok)
        {
            await sender.PushFriendListChangedAsync(
                session.UserId, session.User.Username, requesterUsername, "added");
        }
    }
}