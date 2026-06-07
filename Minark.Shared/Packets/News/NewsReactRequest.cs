namespace Minark.Shared.Packets.News;

/// <summary>Client envoie une réaction (like/dislike/annulation).</summary>
public class NewsReactRequest
{
    public string Token { get; init; } = string.Empty;
    public int NewsId { get; init; }
    public ReactionType Reaction { get; init; } // None = annuler
}