using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Friends;

public sealed class StatusUpdateHandler(
    IAuthService auth,
    IFriendService friends,
    IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.StatusUpdateRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<StatusUpdateRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        await friends.UpdateStatusAsync(session.UserId, req.Status);
        await sender.PushStatusToFriendsAsync(session.UserId, session.User.Username, req.Status);
    }
}