using System.Text.Json.Serialization;

namespace Minark.Client.Models;

/// <summary>Manifeste JSON publié par Minark.Packager sur le serveur Web.</summary>
public sealed class GameManifest
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("publishedAt")]
    public DateTime PublishedAt { get; init; }

    [JsonPropertyName("files")]
    public List<GameFileEntry> Files { get; init; } = [];
}

public sealed class GameFileEntry
{
    /// <summary>Chemin relatif au répertoire d'installation (séparateur '/').</summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty; // ✅ était "=> string.Empty" : jamais désérialisé !

    /// <summary>Hash SHA-256 hex lowercase attendu après extraction.</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty; // ✅ idem

    /// <summary>Taille en octets du fichier extrait.</summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Nom de l'archive tar.gz à télécharger sur le serveur.</summary>
    [JsonPropertyName("archive")]
    public string? Archive { get; set; }
}