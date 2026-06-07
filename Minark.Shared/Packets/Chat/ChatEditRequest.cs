namespace Minark.Shared.Packets.Chat;

public class ChatEditRequest
{
    public string Token { get; init; } = string.Empty;
    public int MessageId { get; init; }
    public string NewContent { get; init; } = string.Empty;
}