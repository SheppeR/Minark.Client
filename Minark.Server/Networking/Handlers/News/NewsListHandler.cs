using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.News;

public sealed class NewsListHandler(IAuthService auth, INewsService news, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.NewsListRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<NewsListRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        await sender.SendAsync(clientGuid, PacketType.NewsListResponse,
            await news.GetNewsAsync(req.Page, req.PageSize, session?.UserId));
    }
}