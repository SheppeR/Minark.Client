using System.Net;
using System.Text.RegularExpressions;

namespace Minark.Server.Helpers;

public static class ContentSanitizer
{
    private const int MAX_MESSAGE_LENGTH = 4096;
    private const int MIN_MESSAGE_LENGTH = 1;
    private const int MAX_NEWS_LENGTH = 10000;

    /// <summary>
    ///     Sanitise le contenu d'un message pour éviter XSS et contrôle la longueur.
    /// </summary>
    public static (bool IsValid, string SanitizedContent, string? ErrorMessage) SanitizeMessage(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return (false, "", "Le message ne peut pas être vide");
        }

        var trimmed = content.Trim();

        if (trimmed.Length < MIN_MESSAGE_LENGTH)
        {
            return (false, "", "Le message est trop court");
        }

        if (trimmed.Length > MAX_MESSAGE_LENGTH)
        {
            return (false, "", $"Le message dépasse {MAX_MESSAGE_LENGTH} caractères");
        }

        // Échapper le HTML
        var escaped = WebUtility.HtmlEncode(trimmed);

        // Préserver les breaks de ligne
        var formatted = escaped
            .Replace("\r\n", "<br/>")
            .Replace("\n", "<br/>")
            .Replace("\r", "");

        return (true, formatted, null);
    }

    /// <summary>
    ///     Sanitise le contenu des articles news (accepte HTML limité).
    /// </summary>
    public static (bool IsValid, string SanitizedContent, string? ErrorMessage) SanitizeNewsContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return (false, "", "Le contenu ne peut pas être vide");
        }

        var trimmed = content.Trim();

        if (trimmed.Length > MAX_NEWS_LENGTH)
        {
            return (false, "", $"Le contenu dépasse {MAX_NEWS_LENGTH} caractères");
        }

        // Permettre les tags spécifiques pour les images
        var allowedTags = new[] { "[img]", "[/img]", "[b]", "[/b]", "[i]", "[/i]" };

        // Échapper tout le contenu
        var content_escaped = WebUtility.HtmlEncode(trimmed);

        // Ré-allowlister les tags
        foreach (var tag in allowedTags)
        {
            var encodedTag = WebUtility.HtmlEncode(tag);
            content_escaped = content_escaped.Replace(encodedTag, tag);
        }

        // Valider les URLs des images
        var imageUrls = Regex.Matches(content_escaped, @"\[img\](.*?)\[/img\]");
        foreach (Match match in imageUrls)
        {
            var url = match.Groups[1].Value;
            if (!IsValidImageUrl(url))
            {
                return (false, "", "URL d'image invalide détectée");
            }
        }

        return (true, content_escaped, null);
    }

    private static bool IsValidImageUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https" &&
               (url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Valide une URL d'avatar.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateAvatarUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return (true, null); // Vide = supprimer l'avatar
        }

        if (url.Length > 2048)
        {
            return (false, "URL trop longue");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return (false, "URL invalide");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return (false, "Seules les URLs HTTP/HTTPS sont acceptées");
        }

        return (true, null);
    }
}