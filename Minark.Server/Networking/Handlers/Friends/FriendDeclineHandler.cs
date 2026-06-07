using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Friends;

public sealed class FriendDeclineHandler(IAuthService auth, IFriendService friends) : IPacketHandler
{
    public PacketType PacketType => PacketType.FriendInviteDecline;

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

        await friends.DeclineFriendRequestAsync(session.UserId, req.FriendshipId);
    }
}