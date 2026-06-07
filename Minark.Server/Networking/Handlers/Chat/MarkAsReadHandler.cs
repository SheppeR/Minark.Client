using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.Chat;

public sealed class MarkAsReadHandler(IAuthService auth, IChatService chat) : IPacketHandler
{
    public PacketType PacketType => PacketType.MarkAsReadRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<MarkAsReadRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        await chat.MarkAsReadAsync(session.UserId, req.FromUsername);
    }
}