namespace Minark.Shared.Packets.Chat;

public class ReactionDto
{
    public string Emoji { get; init; } = string.Empty; // ex: "👍", "❤️", "😂"
    public int Count { get; init; }
    public bool HasMine { get; init; } // l'utilisateur courant a réagi avec cet emoji
}