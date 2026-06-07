namespace Minark.Shared.Packets.Chat;

public class ChatDeleteNotify
{
    public int MessageId { get; init; }
    public string DeletedByUsername { get; set; } = string.Empty;
}