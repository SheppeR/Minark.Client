using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.News;

public sealed class NewsUpsertHandler(INewsService news, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.NewsUpsertRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<NewsUpsertRequest>(payload);
        if (req is null)
        {
            return;
        }

        var resp = await news.UpsertAsync(req);
        await sender.SendAsync(clientGuid, PacketType.NewsUpsertResponse, resp);
        if (resp.Success)
        {
            await news.BroadcastNewsChangedAsync(resp.NewsId, req.Id.HasValue ? "updated" : "created");
        }
    }
}