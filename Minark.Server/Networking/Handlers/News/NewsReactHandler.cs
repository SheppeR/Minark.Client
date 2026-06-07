using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.News;

public sealed class NewsReactHandler(IAuthService auth, INewsService news, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.NewsReactRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<NewsReactRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.NewsReactResponse,
            await news.ReactAsync(session.UserId, req));
    }
}