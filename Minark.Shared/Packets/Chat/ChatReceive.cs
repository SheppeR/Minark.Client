namespace Minark.Shared.Packets.Chat;

public class ChatReceive
{
    public int Id { get; init; } // ID DB du message — requis pour react/edit/delete
    public string FromUsername { get; init; } = string.Empty;
    public string ToUsername { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime SentAt { get; init; } = DateTime.UtcNow;
}