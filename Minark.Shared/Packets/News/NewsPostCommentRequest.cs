namespace Minark.Shared.Packets.News;

public class NewsPostCommentRequest
{
    public string Token { get; init; } = string.Empty;
    public int NewsId { get; init; }
    public string Content { get; init; } = string.Empty;
}