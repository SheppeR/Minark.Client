using Minark.Server.Data.Entities;

// ReSharper disable UnusedMemberInSuper.Global

namespace Minark.Server.Services.Interfaces;

public interface IChatService
{
    Task<ChatMessage?> SaveMessageByUsernameAsync(int senderId, string receiverUsername, string content);
    Task<List<ChatMessageDto>> GetHistoryAsync(int userId, string withUsername, int page = 1, int pageSize = 50);
    Task<bool> HasMoreHistoryAsync(int userId, string withUsername, int page, int pageSize = 50);
    Task AddUnreadAsync(int recipientId, int messageId, string fromUsername);
    Task AddUnreadByUsernameAsync(string recipientUsername, int messageId, string fromUsername);
    Task<Dictionary<string, int>> GetUnreadCountsAsync(int userId);
    Task MarkAsReadAsync(int userId, string fromUsername);
    Task PurgeOldMessagesAsync(int retentionDays = 30);

    // Nouvelles features
    Task<(bool Success, string? Error, int OtherUserId)> DeleteMessageAsync(int messageId, int requesterId);

    Task<(bool Success, string? Error, ChatMessage? Msg, int OtherUserId)> EditMessageAsync(int messageId,
        int requesterId, string newContent);

    Task<List<ChatMessageDto>> SearchMessagesAsync(int userId, string withUsername, string query);
    Task<(List<ReactionDto> Reactions, int OtherUserId)> ToggleReactionAsync(int messageId, int userId, string emoji);
}