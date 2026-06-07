using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Friends;

public sealed class FriendRemoveHandler(
    IAuthService auth,
    IFriendService friends,
    IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.FriendRemove;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<FriendRemove>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        var removed = await friends.RemoveFriendAsync(session.UserId, req.FriendUsername);
        if (removed)
        {
            await sender.PushFriendListChangedAsync(
                session.UserId, session.User.Username, req.FriendUsername, "removed");
        }
    }
}