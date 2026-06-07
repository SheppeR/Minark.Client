using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.News;

public sealed class NewsDeleteHandler(INewsService news) : IPacketHandler
{
    public PacketType PacketType => PacketType.NewsDeleteRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<NewsDeleteRequest>(payload);
        if (req is null)
        {
            return;
        }

        if (await news.DeleteAsync(req.NewsId))
        {
            await news.BroadcastNewsChangedAsync(req.NewsId, "deleted");
        }
    }
}