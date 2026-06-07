namespace Minark.Shared.Packets.News;

/// <summary>Supprimer une news (admin).</summary>
public class NewsDeleteRequest
{
    public string Token { get; set; } = string.Empty;
    public int NewsId { get; set; }
}