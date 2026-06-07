namespace Minark.Shared.Packets.News;

/// <summary>Créer ou mettre à jour une news (admin).</summary>
public class NewsUpsertRequest
{
    public string Token { get; set; } = string.Empty;
    public int? Id { get; set; } // null = création
    public string Title => string.Empty;
    public string Content => string.Empty; // supporte [img]url[/img]
    public string Author => string.Empty;
    public string Category => string.Empty;
    public string? ImageUrl { get; set; }

    /// <summary>Liste ordonnée des médias du carousel.</summary>
    // ReSharper disable once CollectionNeverUpdated.Global
    public List<NewsMediaDto> MediaUrls { get; } = [];
}