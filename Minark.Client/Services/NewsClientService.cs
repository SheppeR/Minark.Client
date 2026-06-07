using System.Collections.Concurrent;
using Minark.Client.Networking;
using Minark.Client.Services.Interfaces;
using Minark.Shared.Packets;
using Minark.Shared.Packets.News;

namespace Minark.Client.Services;

/// <summary>
///     Gère la communication client → serveur pour les actualités.
///     Pattern : handlers permanents + files TCS par type de réponse (évite les race conditions).
/// </summary>
public class NewsClientService : INewsClientService
{
    private readonly ConcurrentQueue<TaskCompletionSource<NewsCommentsResponse>>
        _commentsQueue = new();

    private readonly ConcurrentQueue<TaskCompletionSource<NewsListResponse>>
        _listQueue = new();

    private readonly ConcurrentQueue<TaskCompletionSource<NewsPostCommentResponse>>
        _postCommentQueue = new();

    private readonly ConcurrentQueue<TaskCompletionSource<NewsReactResponse>>
        _reactQueue = new();

    private readonly TcpClientService _tcp;

    public NewsClientService(TcpClientService tcp, PacketDispatcher dispatcher)
    {
        _tcp = tcp;

        dispatcher.Register(PacketType.NewsListResponse,
            p => DequeueAndResolve(p, _listQueue, new NewsListResponse { Success = false }));
        dispatcher.Register(PacketType.NewsReactResponse,
            p => DequeueAndResolve(p, _reactQueue, new NewsReactResponse { Success = false }));
        dispatcher.Register(PacketType.NewsCommentsResponse,
            p => DequeueAndResolve(p, _commentsQueue, new NewsCommentsResponse { Success = false }));
        dispatcher.Register(PacketType.NewsPostCommentResponse,
            p => DequeueAndResolve(p, _postCommentQueue, new NewsPostCommentResponse { Success = false }));
        dispatcher.Register(PacketType.NewsChanged, p => Notify(p, ref OnNewsChanged));
        dispatcher.Register(PacketType.NewsStatsUpdated, p => Notify(p, ref OnNewsStatsUpdated));
    }

    public event Action<NewsChangedNotification>? OnNewsChanged;
    public event Action<NewsStatsUpdated>? OnNewsStatsUpdated;

    public async Task<NewsListResponse> GetNewsAsync(string token, int page = 1, int pageSize = 10)
    {
        var tcs = new TaskCompletionSource<NewsListResponse>();
        _listQueue.Enqueue(tcs);
        await _tcp.SendAsync(PacketType.NewsListRequest,
            new NewsListRequest { Token = token, Page = page, PageSize = pageSize });
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public async Task<NewsReactResponse> ReactAsync(string token, int newsId, ReactionType reaction)
    {
        var tcs = new TaskCompletionSource<NewsReactResponse>();
        _reactQueue.Enqueue(tcs);
        await _tcp.SendAsync(PacketType.NewsReactRequest,
            new NewsReactRequest { Token = token, NewsId = newsId, Reaction = reaction });
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public async Task<NewsCommentsResponse> GetCommentsAsync(string token, int newsId, int page = 1, int pageSize = 3)
    {
        var tcs = new TaskCompletionSource<NewsCommentsResponse>();
        _commentsQueue.Enqueue(tcs);
        await _tcp.SendAsync(PacketType.NewsCommentsRequest,
            new NewsCommentsRequest { Token = token, NewsId = newsId, Page = page, PageSize = pageSize });
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public async Task<NewsPostCommentResponse> PostCommentAsync(string token, int newsId, string content)
    {
        var tcs = new TaskCompletionSource<NewsPostCommentResponse>();
        _postCommentQueue.Enqueue(tcs);
        await _tcp.SendAsync(PacketType.NewsPostCommentRequest,
            new NewsPostCommentRequest { Token = token, NewsId = newsId, Content = content });
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void Notify<T>(string payload, ref Action<T>? ev) where T : class
    {
        var n = PacketSerializer.DeserializePayload<T>(payload);
        if (n is not null)
        {
            ev?.Invoke(n);
        }
    }

    private static void DequeueAndResolve<T>(string payload, ConcurrentQueue<TaskCompletionSource<T>> queue, T fallback)
    {
        var result = PacketSerializer.DeserializePayload<T>(payload) ?? fallback;
        if (queue.TryDequeue(out var tcs))
        {
            tcs.TrySetResult(result);
        }
    }
}