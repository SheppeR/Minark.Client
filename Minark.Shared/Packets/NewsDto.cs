namespace Minark.Shared.Packets;

public class NewsDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Author { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; }
    public string? ImageUrl { get; init; }
    public string Category { get; init; } = string.Empty;

    // Stats chargées avec l'article (dépend de l'utilisateur connecté)
    public int LikeCount { get; init; }
    public int DislikeCount { get; init; }
    public int CommentCount { get; init; }
    public ReactionType UserReaction { get; init; }

    /// <summary>URLs du carousel (images + vidéos), triées par SortOrder.</summary>
    public List<NewsMediaDto> MediaUrls { get; init; } = [];
}