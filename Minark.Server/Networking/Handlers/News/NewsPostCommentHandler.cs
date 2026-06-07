using Minark.Server.Services.Interfaces;

namespace Minark.Server.Networking.Handlers.News;

public sealed class NewsPostCommentHandler(IAuthService auth, INewsService news, IServerSender sender) : IPacketHandler
{
    public PacketType PacketType => PacketType.NewsPostCommentRequest;

    public async Task HandleAsync(Guid clientGuid, string payload)
    {
        var req = PacketSerializer.DeserializePayload<NewsPostCommentRequest>(payload);
        if (req is null || string.IsNullOrWhiteSpace(req.Content))
        {
            return;
        }

        var session = await auth.ValidateTokenAsync(req.Token);
        if (session is null)
        {
            return;
        }

        await sender.SendAsync(clientGuid, PacketType.NewsPostCommentResponse,
            await news.PostCommentAsync(session.UserId, session.User.Username, session.User.AvatarUrl, req));
    }
}