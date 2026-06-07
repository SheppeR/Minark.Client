using System.Collections.Concurrent;
using Minark.Client.Networking;
using Minark.Client.Services.Interfaces;
using Minark.Shared.Packets;
using Minark.Shared.Packets.Chat;

namespace Minark.Client.Services;

public class ChatClientService : IChatClientService
{
    private readonly ConcurrentQueue<TaskCompletionSource<ChatHistoryResponse>>
        _historyQueue = new();

    private readonly ConcurrentQueue<TaskCompletionSource<ChatSearchResponse>>
        _searchQueue = new();

    private readonly TcpClientService _tcp;

    private readonly ConcurrentQueue<TaskCompletionSource<UnreadCountsResponse>>
        _unreadQueue = new();

    public ChatClientService(TcpClientService tcp, PacketDispatcher dispatcher)
    {
        _tcp = tcp;

        dispatcher.Register(PacketType.ChatReceive, p => Notify(p, ref OnMessageReceived));
        dispatcher.Register(PacketType.TypingStart, p => Notify(p, ref OnTypingStarted));
        dispatcher.Register(PacketType.TypingStop, p => Notify(p, ref OnTypingStopped));
        dispatcher.Register(PacketType.ChatDeleteNotify, p => Notify(p, ref OnMessageDeleted));
        dispatcher.Register(PacketType.ChatEditNotify, p => Notify(p, ref OnMessageEdited));
        dispatcher.Register(PacketType.ChatReactNotify, p => Notify(p, ref OnMessageReacted));

        dispatcher.Register(PacketType.ChatHistoryResponse, p => DequeueAndResolve(p, _historyQueue));
        dispatcher.Register(PacketType.ChatSearchResponse, p => DequeueAndResolve(p, _searchQueue));
        dispatcher.Register(PacketType.UnreadCountsResponse, p => DequeueAndResolve(p, _unreadQueue));
    }

    public event Action<ChatReceive>? OnMessageReceived;
    public event Action<TypingNotification>? OnTypingStarted;
    public event Action<TypingNotification>? OnTypingStopped;
    public event Action<ChatDeleteNotify>? OnMessageDeleted;
    public event Action<ChatEditNotify>? OnMessageEdited;
    public event Action<ChatReactNotify>? OnMessageReacted;

    public async Task SendMessageAsync(string token, string toUsername, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        await _tcp.SendAsync(PacketType.ChatSend, new ChatSend
            { Token = token, ToUsername = toUsername, Content = content });
    }

    public async Task SendTypingStartAsync(string toUsername)
    {
        await _tcp.SendAsync(PacketType.TypingStart, new TypingNotification { ToUsername = toUsername });
    }

    public async Task SendTypingStopAsync(string toUsername)
    {
        await _tcp.SendAsync(PacketType.TypingStop, new TypingNotification { ToUsername = toUsername });
    }

    public async Task MarkAsReadAsync(string token, string fromUsername)
    {
        await _tcp.SendAsync(PacketType.MarkAsReadRequest,
            new MarkAsReadRequest { Token = token, FromUsername = fromUsername });
    }

    public async Task DeleteMessageAsync(string token, int messageId)
    {
        await _tcp.SendAsync(PacketType.ChatDeleteRequest,
            new ChatDeleteRequest { Token = token, MessageId = messageId });
    }

    public async Task EditMessageAsync(string token, int messageId, string newContent)
    {
        await _tcp.SendAsync(PacketType.ChatEditRequest,
            new ChatEditRequest { Token = token, MessageId = messageId, NewContent = newContent });
    }

    public async Task ReactToMessageAsync(string token, int messageId, string emoji)
    {
        await _tcp.SendAsync(PacketType.ChatReactRequest,
            new ChatReactRequest { Token = token, MessageId = messageId, Emoji = emoji });
    }

    public async Task<UnreadCountsResponse> GetUnreadCountsAsync(string token)
    {
        var tcs = new TaskCompletionSource<UnreadCountsResponse>();
        _unreadQueue.Enqueue(tcs);
        await _tcp.SendAsync(PacketType.UnreadCountsRequest, new TokenRequest { Token = token });
        try
        {
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch
        {
            return new UnreadCountsResponse { Success = false };
        }
    }

    public async Task<ChatHistoryResponse> GetHistoryAsync(string token, string withUsername, int page = 1)
    {
        var tcs = new TaskCompletionSource<ChatHistoryResponse>();
        _historyQueue.Enqueue(tcs);
        await _tcp.SendAsync(PacketType.ChatHistoryRequest,
            new ChatHistoryRequest { Token = token, WithUsername = withUsername, Page = page, PageSize = 10 });
        return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public async Task<ChatSearchResponse> SearchMessagesAsync(string token, string withUsername, string query)
    {
        var tcs = new TaskCompletionSource<ChatSearchResponse>();
        _searchQueue.Enqueue(tcs);
        await _tcp.SendAsync(PacketType.ChatSearchRequest,
            new ChatSearchRequest { Token = token, WithUsername = withUsername, Query = query });
        try
        {
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch
        {
            return new ChatSearchResponse { Success = false };
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static void Notify<T>(string payload, ref Action<T>? eventField) where T : class
    {
        var msg = PacketSerializer.DeserializePayload<T>(payload);
        if (msg is not null)
        {
            eventField?.Invoke(msg);
        }
    }

    private static void DequeueAndResolve<T>(string payload, ConcurrentQueue<TaskCompletionSource<T>> queue)
        where T : new()
    {
        var result = PacketSerializer.DeserializePayload<T>(payload);
        if (result is not null && queue.TryDequeue(out var tcs))
        {
            tcs.TrySetResult(result);
        }
    }
}