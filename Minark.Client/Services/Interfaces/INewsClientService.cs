using Minark.Shared.Packets;
using Minark.Shared.Packets.News;

namespace Minark.Client.Services.Interfaces;

public interface INewsClientService
{
    event Action<NewsChangedNotification>? OnNewsChanged;
    event Action<NewsStatsUpdated>? OnNewsStatsUpdated;

    Task<NewsListResponse> GetNewsAsync(string token, int page = 1, int pageSize = 10);
    Task<NewsReactResponse> ReactAsync(string token, int newsId, ReactionType reaction);
    Task<NewsCommentsResponse> GetCommentsAsync(string token, int newsId, int page = 1, int pageSize = 3);
    Task<NewsPostCommentResponse> PostCommentAsync(string token, int newsId, string content);
}