namespace Minark.Shared.Packets.Chat;

public class ChatSearchResponse
{
    public bool Success { get; init; }
    public List<ChatMessageDto> Results { get; init; } = [];
}