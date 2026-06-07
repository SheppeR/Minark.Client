namespace Minark.Shared.Packets.News;

public class NewsListRequest
{
    public string Token { get; init; } = string.Empty;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
}