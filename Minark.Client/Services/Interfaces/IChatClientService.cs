using Minark.Shared.Packets.Chat;

namespace Minark.Client.Services.Interfaces;

public interface IChatClientService
{
    event Action<ChatReceive>? OnMessageReceived;
    event Action<TypingNotification>? OnTypingStarted;
    event Action<TypingNotification>? OnTypingStopped;
    event Action<ChatDeleteNotify>? OnMessageDeleted;
    event Action<ChatEditNotify>? OnMessageEdited;
    event Action<ChatReactNotify>? OnMessageReacted;

    Task SendMessageAsync(string token, string toUsername, string content);
    Task SendTypingStartAsync(string toUsername);
    Task SendTypingStopAsync(string toUsername);
    Task<ChatHistoryResponse> GetHistoryAsync(string token, string withUsername, int page = 1);
    Task MarkAsReadAsync(string token, string fromUsername);
    Task<UnreadCountsResponse> GetUnreadCountsAsync(string token);

    // Nouvelles features
    Task DeleteMessageAsync(string token, int messageId);
    Task EditMessageAsync(string token, int messageId, string newContent);
    Task<ChatSearchResponse> SearchMessagesAsync(string token, string withUsername, string query);
    Task ReactToMessageAsync(string token, int messageId, string emoji);
}