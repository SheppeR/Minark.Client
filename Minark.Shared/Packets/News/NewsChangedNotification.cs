namespace Minark.Shared.Packets.News;

/// <summary>Push du serveur vers tous les clients quand une news est créée ou modifiée.</summary>
public class NewsChangedNotification
{
    public int NewsId { get; set; }
    public string EventType { get; set; } = "created"; // "created" | "updated" | "deleted"
}