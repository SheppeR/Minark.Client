namespace Minark.Shared.Packets.News;

/// <summary>
///     Push serveur → client : nouvelles stats après une réaction ou un commentaire.
///     Permet la mise à jour en temps réel pour tous les clients connectés.
/// </summary>
public class NewsStatsUpdated
{
    public int NewsId { get; init; }
    public int LikeCount { get; init; }
    public int DislikeCount { get; init; }
    public int CommentCount { get; init; }
}