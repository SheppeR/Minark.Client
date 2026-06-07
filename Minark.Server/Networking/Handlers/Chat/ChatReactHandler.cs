using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Chat;

public sealed class ChatReactHandler(
    IAuthService auth,
    IChatService chat,
    ISessionStore sessionStore,
    IServerSender sender,
    ILogger<ChatReactHandler> logger) : IPacketHandler
{
    public PacketType PacketType => PacketType.ChatReactRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<ChatReactRequest>(payload);
        if (req is null || string.IsNullOrWhiteSpace(req.Emoji))
        {
            logger.LogWarning("[REACT] Payload invalide ou emoji vide");
            return;
        }

        logger.LogInformation("[REACT] msgId={MsgId} emoji={Emoji}", req.MessageId, req.Emoji);

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        var (reactions, otherUserId) = await chat.ToggleReactionAsync(req.MessageId, session.UserId, req.Emoji);

        logger.LogInformation("[REACT] msgId={MsgId} userId={UserId} → {Count} reactions",
            req.MessageId, session.UserId, reactions.Count);

        var notify = new ChatReactNotify { MessageId = req.MessageId, Reactions = reactions };
        await sender.SendAsync(clientGuid, PacketType.ChatReactNotify, notify);

        var otherGuid = sessionStore.FindClientByUserId(otherUserId);
        if (otherGuid.HasValue)
        {
            await sender.SendAsync(otherGuid.Value, PacketType.ChatReactNotify, notify);
        }
    }
}