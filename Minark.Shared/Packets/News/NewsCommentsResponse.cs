namespace Minark.Shared.Packets.News;

public class NewsCommentsResponse
{
    public bool Success { get; init; }
    public List<NewsCommentDto> Comments { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
}