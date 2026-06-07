using Microsoft.Extensions.Configuration;

namespace Minark.Client.Services;

/// <summary>
///     Résout les URLs relatives du site web en URLs absolues.
///     Ex : /uploads/news/img.jpg → http://localhost:8080/uploads/news/img.jpg
/// </summary>
public static class WebConfig
{
    public static string BaseUrl { get; private set; } = "http://localhost:8080";

    public static void Init(IConfiguration config)
    {
        var url = config["Web:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(url))
        {
            BaseUrl = url.TrimEnd('/');
        }
    }

    /// <summary>
    ///     Si l'URL est relative (/uploads/...), la rend absolue avec la base configurée.
    ///     Si elle est déjà absolue (http://...), la retourne telle quelle.
    /// </summary>
    public static string Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (url.StartsWith("http://") || url.StartsWith("https://"))
        {
            return url;
        }

        // URL relative → préfixer avec la base web
        return BaseUrl + (url.StartsWith('/') ? url : "/" + url);
    }
}