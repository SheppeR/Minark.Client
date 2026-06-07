using Minark.Server.Helpers;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Chat;

public sealed class ChatSendHandler(
    IAuthService auth,
    IChatService chat,
    ISessionStore sessionStore,
    IServerSender sender,
    ILogger<ChatSendHandler> logger) : IPacketHandler
{
    public PacketType PacketType => PacketType.ChatSend;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<ChatSend>(payload);
        if (req is null)
        {
            logger.LogWarning("ChatSend: Invalid payload");
            return;
        }

        // ✅ Validation et sanitisation du contenu
        var (isValid, sanitized, error) = ContentSanitizer.SanitizeMessage(req.Content);
        if (!isValid)
        {
            logger.LogWarning("ChatSend: {Error}", error);
            return;
        }

        req.Content = sanitized;

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            logger.LogWarning("ChatSend: token invalide depuis {Client}", clientGuid);
            return;
        }

        var saved = await chat.SaveMessageByUsernameAsync(session.UserId, req.ToUsername, req.Content);
        if (saved is null)
        {
            logger.LogWarning("ChatSend: message non sauvegardé ({From} → {To})",
                session.User.Username, req.ToUsername);
            return;
        }

        var packet = new ChatReceive
        {
            Id = saved.Id,
            FromUsername = session.User.Username,
            ToUsername = req.ToUsername,
            Content = req.Content,
            SentAt = saved.SentAt
        };

        // ✅ Utiliser GetClientByUsername au lieu de boucle O(n)
        var recipient = sessionStore.GetClientByUsername(req.ToUsername);

        if (recipient.HasValue)
        {
            await sender.SendAsync(recipient.Value.ClientGuid, PacketType.ChatReceive, packet);
        }
        else
        {
            await chat.AddUnreadByUsernameAsync(req.ToUsername, saved.Id, session.User.Username);
        }

        await sender.SendAsync(clientGuid, PacketType.ChatReceive, packet);
        logger.LogDebug("Chat: {From} → {To}", session.User.Username, req.ToUsername);
    }
}