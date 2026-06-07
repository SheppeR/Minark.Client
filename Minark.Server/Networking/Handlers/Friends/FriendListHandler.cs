using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Friends;

public sealed class FriendListHandler(IAuthService auth, IFriendService friends, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.FriendListRequest;

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
            await sender.SendAsync(clientGuid, PacketType.FriendListResponse,
                new FriendListResponse { Success = false, ErrorMessage = "Token invalide." });
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.FriendListResponse,
            await friends.GetFriendsAsync(session.UserId));
    }
}