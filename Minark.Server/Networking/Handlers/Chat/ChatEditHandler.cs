using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Chat;

public sealed class ChatEditHandler(
    IAuthService auth,
    IChatService chat,
    ISessionStore sessionStore,
    IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.ChatEditRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<ChatEditRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        var (success, error, msg, otherUserId) =
            await chat.EditMessageAsync(req.MessageId, session.UserId, req.NewContent);
        if (!success || msg is null)
        {
            await sender.SendAsync(clientGuid, PacketType.Error, new { Message = error });
            return;
        }

        var notify = new ChatEditNotify
            { MessageId = msg.Id, NewContent = msg.Content, EditedByUsername = session.User.Username };
        await sender.SendAsync(clientGuid, PacketType.ChatEditNotify, notify);

        var otherGuid = sessionStore.FindClientByUserId(otherUserId);
        if (otherGuid.HasValue)
        {
            await sender.SendAsync(otherGuid.Value, PacketType.ChatEditNotify, notify);
        }
    }
}