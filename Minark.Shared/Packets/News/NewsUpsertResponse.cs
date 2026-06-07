namespace Minark.Shared.Packets.News;

public class NewsUpsertResponse
{
    public bool Success { get; init; }
    public int NewsId { get; init; }
    public string? ErrorMessage { get; set; }
}