namespace Minark.Shared.Packets.News;

public class NewsCommentsRequest
{
    public string Token { get; init; } = string.Empty;
    public int NewsId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 3;
}