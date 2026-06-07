using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Chat;

public sealed class ChatHistoryHandler(IAuthService auth, IChatService chat, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.ChatHistoryRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<ChatHistoryRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            await sender.SendAsync(clientGuid, PacketType.ChatHistoryResponse,
                new ChatHistoryResponse { Success = false });
            return;
        }

        var messages = await chat.GetHistoryAsync(session.UserId, req.WithUsername, req.Page, req.PageSize);
        var hasMore = messages.Count >= req.PageSize
                      && await chat.HasMoreHistoryAsync(session.UserId, req.WithUsername, req.Page, req.PageSize);

        await sender.SendAsync(clientGuid, PacketType.ChatHistoryResponse,
            new ChatHistoryResponse { Success = true, Messages = messages, HasMore = hasMore });
    }
}