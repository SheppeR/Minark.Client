using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Chat;

/// <summary>Gère TypingStart et TypingStop — le PacketType est injecté via le constructeur.</summary>
public sealed class TypingHandler(ISessionStore sessionStore, IServerSender sender, PacketType packetType)
    : IPacketHandler
{
    public PacketType PacketType => packetType;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var senderInfo = sessionStore.GetUser(clientGuid);
        if (senderInfo is null)
        {
            return;
        }

        var req = PacketSerializer.DeserializePayload<TypingNotification>(payload);
        if (req is null)
        {
            return;
        }

        req.FromUsername = senderInfo.Value.Username;

        var target = sessionStore.GetClientByUsername(req.ToUsername);
        if (target.HasValue)
        {
            await sender.SendAsync(target.Value.ClientGuid, packetType, req);
        }
    }
}