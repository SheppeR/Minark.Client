using Microsoft.EntityFrameworkCore;
using Minark.Server.Data;
using Minark.Server.Data.Entities;
using Minark.Server.Networking;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Services;

public class NewsService(AppDbContext db, IClientBroadcaster broadcaster) : INewsService
{
    public async Task<NewsListResponse> GetNewsAsync(int page, int pageSize, int? userId = null)
    {
        var total = await db.News.CountAsync(n => n.IsPublished);

        var news = await db.News
            .Where(n => n.IsPublished)
            .OrderByDescending(n => n.PublishedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NewsDto
            {
                Id = n.Id,
                Title = n.Title,
                Content = n.Content,
                Author = n.Author,
                Category = n.Category,
                ImageUrl = n.ImageUrl,
                PublishedAt = n.PublishedAt,
                LikeCount = db.NewsReactions.Count(r => r.NewsId == n.Id && r.Reaction == 1),
                DislikeCount = db.NewsReactions.Count(r => r.NewsId == n.Id && r.Reaction == 2),
                CommentCount = db.NewsComments.Count(c => c.NewsId == n.Id),
                UserReaction = userId == null
                    ? ReactionType.None
                    : (ReactionType)db.NewsReactions
                        .Where(r => r.NewsId == n.Id && r.UserId == userId)
                        .Select(r => r.Reaction)
                        .FirstOrDefault(),
                MediaUrls = db.NewsMedias
                    .Where(m => m.NewsId == n.Id)
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new NewsMediaDto { Url = m.Url, MediaType = m.MediaType })
                    .ToList()
            })
            .ToListAsync();

        return new NewsListResponse { Success = true, News = news, TotalCount = total };
    }

    public async Task BroadcastNewsChangedAsync(int newsId, string eventType)
    {
        var notification = new NewsChangedNotification { NewsId = newsId, EventType = eventType };
        await broadcaster.BroadcastAsync(PacketSerializer.Serialize(PacketType.NewsChanged, notification));
    }

    public async Task<NewsUpsertResponse> UpsertAsync(NewsUpsertRequest req)
    {
        try
        {
            News entity;
            if (req.Id.HasValue)
            {
                entity = await db.News.FindAsync(req.Id.Value)
                         ?? throw new InvalidOperationException("News introuvable.");
            }
            else
            {
                entity = new News();
                db.News.Add(entity);
            }

            entity.Title = req.Title.Trim();
            entity.Content = req.Content;
            entity.Author = req.Author.Trim();
            entity.Category = req.Category.Trim();
            entity.ImageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim();
            entity.IsPublished = true;
            if (!req.Id.HasValue)
            {
                entity.PublishedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();

            if (req.MediaUrls is { Count: > 0 })
            {
                await db.NewsMedias.Where(m => m.NewsId == entity.Id).ExecuteDeleteAsync();
                for (var i = 0; i < req.MediaUrls.Count; i++)
                {
                    db.NewsMedias.Add(new NewsMedia
                    {
                        NewsId = entity.Id,
                        Url = req.MediaUrls[i].Url,
                        MediaType = req.MediaUrls[i].MediaType,
                        SortOrder = i
                    });
                }

                await db.SaveChangesAsync();
            }

            return new NewsUpsertResponse { Success = true, NewsId = entity.Id };
        }
        catch (Exception ex)
        {
            return new NewsUpsertResponse { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<bool> DeleteAsync(int newsId)
    {
        var deleted = await db.News.Where(n => n.Id == newsId).ExecuteDeleteAsync();
        return deleted > 0;
    }

    // ── Reactions ─────────────────────────────────────────────────────────────

    public async Task<NewsReactResponse> ReactAsync(int userId, NewsReactRequest req)
    {
        var existing = await db.NewsReactions
            .FirstOrDefaultAsync(r => r.NewsId == req.NewsId && r.UserId == userId);

        if (req.Reaction == ReactionType.None)
        {
            if (existing is not null)
            {
                db.NewsReactions.Remove(existing);
            }
        }
        else
        {
            var value = (int)req.Reaction;
            if (existing is null)
            {
                db.NewsReactions.Add(new NewsReaction { NewsId = req.NewsId, UserId = userId, Reaction = value });
            }
            else
            {
                existing.Reaction = value;
            }
        }

        await db.SaveChangesAsync();

        var stats = await GetStatsAsync(req.NewsId);
        await broadcaster.BroadcastAsync(PacketSerializer.Serialize(PacketType.NewsStatsUpdated, stats));

        return new NewsReactResponse
        {
            Success = true,
            LikeCount = stats.LikeCount,
            DislikeCount = stats.DislikeCount,
            UserReaction = req.Reaction
        };
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    public async Task<NewsCommentsResponse> GetCommentsAsync(int newsId, int page, int pageSize)
    {
        var total = await db.NewsComments.CountAsync(c => c.NewsId == newsId);

        var comments = await db.NewsComments
            .Where(c => c.NewsId == newsId)
            .OrderBy(c => c.PostedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new NewsCommentDto
            {
                Id = c.Id,
                Username = c.User.Username,
                AvatarUrl = c.User.AvatarUrl,
                Content = c.Content,
                PostedAt = c.PostedAt
            })
            .ToListAsync();

        return new NewsCommentsResponse { Success = true, Comments = comments, TotalCount = total, Page = page };
    }

    public async Task<NewsPostCommentResponse> PostCommentAsync(int userId, string username, string? avatarUrl,
        NewsPostCommentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Content))
        {
            return new NewsPostCommentResponse { Success = false, ErrorMessage = "Commentaire vide." };
        }

        if (req.Content.Length > 1000)
        {
            return new NewsPostCommentResponse
                { Success = false, ErrorMessage = "Commentaire trop long (max 1000 caractères)." };
        }

        var comment = new NewsComment
        {
            NewsId = req.NewsId,
            UserId = userId,
            Content = req.Content.Trim(),
            PostedAt = DateTime.UtcNow
        };
        db.NewsComments.Add(comment);
        await db.SaveChangesAsync();

        var stats = await GetStatsAsync(req.NewsId);
        await broadcaster.BroadcastAsync(PacketSerializer.Serialize(PacketType.NewsStatsUpdated, stats));

        return new NewsPostCommentResponse
        {
            Success = true,
            Comment = new NewsCommentDto
            {
                Id = comment.Id,
                Username = username,
                AvatarUrl = avatarUrl,
                Content = comment.Content,
                PostedAt = comment.PostedAt
            }
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Single query: likes + dislikes + comment count in one round trip.</summary>
    private async Task<NewsStatsUpdated> GetStatsAsync(int newsId)
    {
        var stats = await db.News
            .AsNoTracking()
            .Where(n => n.Id == newsId)
            .Select(n => new
            {
                Likes = db.NewsReactions.Count(r => r.NewsId == newsId && r.Reaction == 1),
                Dislikes = db.NewsReactions.Count(r => r.NewsId == newsId && r.Reaction == 2),
                Comments = db.NewsComments.Count(c => c.NewsId == newsId)
            })
            .FirstOrDefaultAsync();

        return new NewsStatsUpdated
        {
            NewsId = newsId,
            LikeCount = stats?.Likes ?? 0,
            DislikeCount = stats?.Dislikes ?? 0,
            CommentCount = stats?.Comments ?? 0
        };
    }
}