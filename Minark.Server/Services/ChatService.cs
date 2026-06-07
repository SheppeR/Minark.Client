using Microsoft.EntityFrameworkCore;
using Minark.Server.Data;
using Minark.Server.Data.Entities;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Services;

public class ChatService(AppDbContext db) : IChatService
{
    public async Task<ChatMessage?> SaveMessageByUsernameAsync(int senderId, string receiverUsername, string content)
    {
        var receiverId = await db.Users
            .Where(u => u.Username == receiverUsername)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();

        if (receiverId is null)
        {
            return null;
        }

        var isBlocked = await db.BlockedUsers.AnyAsync(b =>
            (b.BlockerId == senderId && b.BlockedId == receiverId.Value) ||
            (b.BlockerId == receiverId.Value && b.BlockedId == senderId));
        if (isBlocked)
        {
            return null;
        }

        var msg = new ChatMessage
        {
            SenderId = senderId,
            ReceiverId = receiverId.Value,
            Content = content,
            SentAt = DateTime.UtcNow
        };
        db.ChatMessages.Add(msg);
        await db.SaveChangesAsync();
        return msg;
    }

    public async Task<List<ChatMessageDto>> GetHistoryAsync(int userId, string withUsername, int page = 1, int pageSize = 50)
    {
        var otherId = await ResolveUserIdAsync(withUsername);
        if (otherId is null)
        {
            return [];
        }

        var raw = await db.ChatMessages
            .AsNoTracking()
            .AsSplitQuery()
            .Where(m =>
                (m.SenderId == userId && m.ReceiverId == otherId.Value) ||
                (m.SenderId == otherId.Value && m.ReceiverId == userId))
            .Include(m => m.Reactions)
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id, m.Content, m.SentAt, m.IsDeleted, m.IsEdited,
                IsOwn = m.SenderId == userId,
                Sender = m.Sender.Username,
                m.Reactions
            })
            .ToListAsync();

        raw.Reverse();

        return raw.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            FromUsername = m.Sender,
            Content = m.IsDeleted ? "" : m.Content,
            SentAt = m.SentAt,
            IsOwn = m.IsOwn,
            IsDeleted = m.IsDeleted,
            IsEdited = m.IsEdited,
            Reactions = BuildReactions(m.Reactions.ToList(), userId)
        }).ToList();
    }

    public async Task<bool> HasMoreHistoryAsync(int userId, string withUsername, int page, int pageSize = 50)
    {
        var otherId = await ResolveUserIdAsync(withUsername);
        if (otherId is null)
        {
            return false;
        }

        var total = await db.ChatMessages
            .AsNoTracking()
            .CountAsync(m =>
                (m.SenderId == userId && m.ReceiverId == otherId.Value) ||
                (m.SenderId == otherId.Value && m.ReceiverId == userId));

        return total > page * pageSize;
    }

    public async Task AddUnreadAsync(int recipientId, int messageId, string fromUsername)
    {
        if (await db.UnreadMessages.AnyAsync(u => u.RecipientId == recipientId && u.MessageId == messageId))
        {
            return;
        }

        db.UnreadMessages.Add(new UnreadMessage
        {
            RecipientId = recipientId,
            MessageId = messageId,
            FromUsername = fromUsername,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task AddUnreadByUsernameAsync(string recipientUsername, int messageId, string fromUsername)
    {
        var recipientId = await ResolveUserIdAsync(recipientUsername);
        if (recipientId is null)
        {
            return;
        }

        await AddUnreadAsync(recipientId.Value, messageId, fromUsername);
    }

    public async Task<Dictionary<string, int>> GetUnreadCountsAsync(int userId)
    {
        return await db.UnreadMessages
            .Where(u => u.RecipientId == userId)
            .GroupBy(u => u.FromUsername)
            .Select(g => new { From = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.From, x => x.Count);
    }

    public async Task MarkAsReadAsync(int userId, string fromUsername)
    {
        await db.UnreadMessages
            .Where(u => u.RecipientId == userId && u.FromUsername == fromUsername)
            .ExecuteDeleteAsync();
    }

    public async Task PurgeOldMessagesAsync(int retentionDays = 30)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        await db.ChatMessages
            .Where(m => m.SentAt < cutoff && !db.UnreadMessages.Any(u => u.MessageId == m.Id))
            .ExecuteDeleteAsync();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error, int OtherUserId)> DeleteMessageAsync(int messageId, int requesterId)
    {
        var msg = await db.ChatMessages.FindAsync(messageId);
        if (msg is null)
        {
            return (false, "Message introuvable.", 0);
        }

        if (msg.SenderId != requesterId)
        {
            return (false, "Vous ne pouvez supprimer que vos propres messages.", 0);
        }

        msg.IsDeleted = true;
        msg.Content = "";
        await db.SaveChangesAsync();
        return (true, null, msg.ReceiverId);
    }

    // ── Edit ──────────────────────────────────────────────────────────────────

    public async Task<(bool Success, string? Error, ChatMessage? Msg, int OtherUserId)> EditMessageAsync(
        int messageId, int requesterId, string newContent)
    {
        if (string.IsNullOrWhiteSpace(newContent))
        {
            return (false, "Contenu vide.", null, 0);
        }

        var msg = await db.ChatMessages.FindAsync(messageId);
        if (msg is null)
        {
            return (false, "Message introuvable.", null, 0);
        }

        if (msg.IsDeleted)
        {
            return (false, "Impossible d'éditer un message supprimé.", null, 0);
        }

        if (msg.SenderId != requesterId)
        {
            return (false, "Vous ne pouvez modifier que vos propres messages.", null, 0);
        }

        msg.Content = newContent.Trim();
        msg.IsEdited = true;
        await db.SaveChangesAsync();
        return (true, null, msg, msg.ReceiverId);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    public async Task<List<ChatMessageDto>> SearchMessagesAsync(int userId, string withUsername, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var otherId = await ResolveUserIdAsync(withUsername);
        if (otherId is null)
        {
            return [];
        }

        var q = query.Trim().ToLower();
        return await db.ChatMessages
            .Where(m =>
                !m.IsDeleted &&
                ((m.SenderId == userId && m.ReceiverId == otherId.Value) ||
                 (m.SenderId == otherId.Value && m.ReceiverId == userId)) &&
                m.Content.ToLower().Contains(q))
            .OrderByDescending(m => m.SentAt)
            .Take(50)
            .Select(m => new ChatMessageDto
            {
                Id = m.Id,
                FromUsername = m.Sender.Username,
                Content = m.Content,
                SentAt = m.SentAt,
                IsOwn = m.SenderId == userId,
                IsEdited = m.IsEdited
            })
            .ToListAsync();
    }

    // ── Reactions ─────────────────────────────────────────────────────────────

    public async Task<(List<ReactionDto> Reactions, int OtherUserId)> ToggleReactionAsync(
        int messageId, int userId, string emoji)
    {
        var msgInfo = await db.ChatMessages
            .AsNoTracking()
            .Where(m => m.Id == messageId)
            .Select(m => new { m.SenderId, m.ReceiverId })
            .FirstOrDefaultAsync();

        if (msgInfo is null)
        {
            return ([], 0);
        }

        var otherUserId = msgInfo.SenderId == userId ? msgInfo.ReceiverId : msgInfo.SenderId;

        var existing = await db.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

        if (existing is not null)
        {
            db.MessageReactions.Remove(existing);
        }
        else
        {
            db.MessageReactions.Add(new MessageReaction
            {
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        var allReactions = await db.MessageReactions
            .AsNoTracking()
            .Where(r => r.MessageId == messageId)
            .ToListAsync();

        return (BuildReactions(allReactions, userId), otherUserId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int?> ResolveUserIdAsync(string username)
    {
        return await db.Users
            .AsNoTracking()
            .Where(u => u.Username == username)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync();
    }

    private static List<ReactionDto> BuildReactions(List<MessageReaction> reactions, int currentUserId)
    {
        return reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new ReactionDto
            {
                Emoji = g.Key,
                Count = g.Count(),
                HasMine = g.Any(r => r.UserId == currentUserId)
            })
            .OrderBy(r => r.Emoji)
            .ToList();
    }
}