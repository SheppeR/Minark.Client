using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Chat;

public sealed class ChatSearchHandler(IAuthService auth, IChatService chat, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.ChatSearchRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<ChatSearchRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        var results = await chat.SearchMessagesAsync(session.UserId, req.WithUsername, req.Query);
        await sender.SendAsync(clientGuid, PacketType.ChatSearchResponse,
            new ChatSearchResponse { Success = true, Results = results });
    }
}