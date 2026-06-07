namespace Minark.Shared.Packets;

public class NewsCommentDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
}