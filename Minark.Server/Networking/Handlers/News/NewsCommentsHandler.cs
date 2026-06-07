using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.News;

public sealed class NewsCommentsHandler(IAuthService auth, INewsService news, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.NewsCommentsRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<NewsCommentsRequest>(payload);
        if (req is null)
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.NewsCommentsResponse,
            await news.GetCommentsAsync(req.NewsId, req.Page, req.PageSize));
    }
}