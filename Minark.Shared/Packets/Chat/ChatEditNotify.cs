namespace Minark.Shared.Packets.Chat;

public class ChatEditNotify
{
    public int MessageId { get; init; }
    public string NewContent { get; init; } = string.Empty;
    public string EditedByUsername { get; set; } = string.Empty;
}