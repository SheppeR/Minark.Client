namespace Minark.Shared.Packets.News;

public class NewsReactResponse
{
    public bool Success { get; init; }
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
    public ReactionType UserReaction { get; init; }
}