namespace Minark.Shared.Packets.Chat;

public class ChatReactNotify
{
    public int MessageId { get; init; }
    public List<ReactionDto> Reactions { get; init; } = [];
}