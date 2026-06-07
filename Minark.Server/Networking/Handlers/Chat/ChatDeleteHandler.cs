using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Chat;

public sealed class ChatDeleteHandler(
    IAuthService auth,
    IChatService chat,
    ISessionStore sessionStore,
    IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.ChatDeleteRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<ChatDeleteRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        var (success, error, otherUserId) = await chat.DeleteMessageAsync(req.MessageId, session.UserId);
        if (!success)
        {
            await sender.SendAsync(clientGuid, PacketType.Error, new { Message = error });
            return;
        }

        var notify = new ChatDeleteNotify { MessageId = req.MessageId, DeletedByUsername = session.User.Username };
        await sender.SendAsync(clientGuid, PacketType.ChatDeleteNotify, notify);

        var otherGuid = sessionStore.FindClientByUserId(otherUserId);
        if (otherGuid.HasValue)
        {
            await sender.SendAsync(otherGuid.Value, PacketType.ChatDeleteNotify, notify);
        }
    }
}