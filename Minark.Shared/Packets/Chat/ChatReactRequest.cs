namespace Minark.Shared.Packets.Chat;

public class ChatReactRequest
{
    public string Token { get; init; } = string.Empty;
    public int MessageId { get; init; }
    public string Emoji { get; init; } = string.Empty; // ex: "👍"
}