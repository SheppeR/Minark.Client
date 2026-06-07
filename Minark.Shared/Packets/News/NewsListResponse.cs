namespace Minark.Shared.Packets.News;

public class NewsListResponse
{
    public bool Success { get; init; }
    public List<NewsDto> News { get; init; } = [];
    public int TotalCount { get; init; }
    public string? ErrorMessage { get; init; }
}