using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Chat;

public sealed class UnreadCountsHandler(IAuthService auth, IChatService chat, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.UnreadCountsRequest;

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
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.UnreadCountsResponse,
            new UnreadCountsResponse { Success = true, Counts = await chat.GetUnreadCountsAsync(session.UserId) });
    }
}