using System.Text.Json.Serialization;

namespace Minark.Packager;

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
    /// <summary>Chemin relatif au dossier jeu, séparateur '/'.</summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>SHA-256 hex du fichier original (avant archivage).</summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>Taille en octets du fichier original.</summary>
    [JsonPropertyName("size")]
    public long Size { get; set; }

    /// <summary>Nom de l'archive .tar.gz à télécharger.</summary>
    [JsonPropertyName("archive")]
    public string Archive { get; init; } = string.Empty;
}