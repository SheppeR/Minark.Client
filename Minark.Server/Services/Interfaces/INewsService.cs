namespace Minark.Server.Services.Interfaces;

public interface INewsService
{
    Task<NewsListResponse> GetNewsAsync(int page, int pageSize, int? userId = null);
    Task BroadcastNewsChangedAsync(int newsId, string eventType);
    Task<NewsUpsertResponse> UpsertAsync(NewsUpsertRequest req);
    Task<bool> DeleteAsync(int newsId);
    Task<NewsReactResponse> ReactAsync(int userId, NewsReactRequest req);
    Task<NewsCommentsResponse> GetCommentsAsync(int newsId, int page, int pageSize);

    Task<NewsPostCommentResponse> PostCommentAsync(int userId, string username, string? avatarUrl,
        NewsPostCommentRequest req);
}