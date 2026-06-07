namespace Minark.Shared.Packets.Chat;

public class ChatDeleteRequest
{
    public string Token { get; init; } = string.Empty;
    public int MessageId { get; init; }
}