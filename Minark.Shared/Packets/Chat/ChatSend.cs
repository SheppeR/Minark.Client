namespace Minark.Shared.Packets.Chat;

public class ChatSend
{
    public string Token { get; init; } = string.Empty;
    public string ToUsername { get; init; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}