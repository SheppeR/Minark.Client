namespace Minark.Shared.Packets.Chat;

public class ChatHistoryResponse
{
    public bool Success { get; init; }
    public List<ChatMessageDto> Messages { get; init; } = [];

    /// <summary>True si le serveur a d'autres pages après celle-ci.</summary>
    public bool HasMore { get; init; }
}