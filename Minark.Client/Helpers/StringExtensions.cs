namespace Minark.Client.Helpers;

public static class StringExtensions
{
    /// <summary>
    ///     Retourne les 1 ou 2 premières lettres en majuscule, ou "?" si la chaîne est vide.
    ///     Utilisé pour les avatars initiales partout dans l'UI.
    /// </summary>
    public static string ToInitials(this string? value)
    {
        return string.IsNullOrEmpty(value) ? "?" : value[..Math.Min(2, value.Length)].ToUpper();
    }
}