namespace Minark.Shared.Packets.News;

public class NewsPostCommentResponse
{
    public bool Success { get; init; }
    public NewsCommentDto? Comment { get; init; }
    public string? ErrorMessage { get; set; }
}