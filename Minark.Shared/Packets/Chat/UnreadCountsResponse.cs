namespace Minark.Shared.Packets.Chat;

/// <summary>Réponse avec le count de messages non lus par ami.</summary>
public class UnreadCountsResponse
{
    public bool Success { get; init; }
    public Dictionary<string, int> Counts { get; init; } = new(); // username → count
}