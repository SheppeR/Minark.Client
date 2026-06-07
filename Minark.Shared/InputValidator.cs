using System.Text.RegularExpressions;

namespace Minark.Shared;

/// <summary>
///     Règles de validation partagées client ↔ serveur.
///     Toute contrainte définie ici s'applique des deux côtés sans duplication.
/// </summary>
public static class InputValidator
{
    // ── Constantes ─────────────────────────────────────────────────────────────

    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 50;
    public const int PasswordMinLength = 6;
    public const int PasswordMaxLength = 128;
    public const int EmailMaxLength = 200;
    public const int MessageMaxLength = 4096;
    public const int CommentMaxLength = 1000;

    // Username : lettres, chiffres, tiret, underscore — pas d'espace
    private static readonly Regex UsernameRegex =
        new(@"^[a-zA-Z0-9_\-]+$", RegexOptions.Compiled);

    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    // ── Username ───────────────────────────────────────────────────────────────

    public static ValidationResult ValidateUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fail("Le pseudo ne peut pas être vide.");
        }

        var v = value.Trim();

        if (v.Length < UsernameMinLength)
        {
            return Fail($"Le pseudo doit contenir au moins {UsernameMinLength} caractères.");
        }

        if (v.Length > UsernameMaxLength)
        {
            return Fail($"Le pseudo ne peut pas dépasser {UsernameMaxLength} caractères.");
        }

        if (!UsernameRegex.IsMatch(v))
        {
            return Fail("Le pseudo ne peut contenir que des lettres, chiffres, - et _.");
        }

        return Ok();
    }

    // ── Email ──────────────────────────────────────────────────────────────────

    public static ValidationResult ValidateEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fail("L'adresse e-mail ne peut pas être vide.");
        }

        var v = value.Trim();

        if (v.Length > EmailMaxLength)
        {
            return Fail($"L'adresse e-mail ne peut pas dépasser {EmailMaxLength} caractères.");
        }

        if (!EmailRegex.IsMatch(v))
        {
            return Fail("L'adresse e-mail n'est pas valide.");
        }

        return Ok();
    }

    // ── Password ───────────────────────────────────────────────────────────────

    public static ValidationResult ValidatePassword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fail("Le mot de passe ne peut pas être vide.");
        }

        if (value.Length < PasswordMinLength)
        {
            return Fail($"Le mot de passe doit contenir au moins {PasswordMinLength} caractères.");
        }

        if (value.Length > PasswordMaxLength)
        {
            return Fail($"Le mot de passe ne peut pas dépasser {PasswordMaxLength} caractères.");
        }

        return Ok();
    }

    public static ValidationResult ValidatePasswordConfirm(string? password, string? confirm)
    {
        var baseCheck = ValidatePassword(password);
        if (!baseCheck.IsValid)
        {
            return baseCheck;
        }

        if (password != confirm)
        {
            return Fail("Les mots de passe ne correspondent pas.");
        }

        return Ok();
    }

    // ── Message ────────────────────────────────────────────────────────────────

    public static ValidationResult ValidateMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fail("Le message ne peut pas être vide.");
        }

        if (value.Length > MessageMaxLength)
        {
            return Fail($"Le message dépasse la limite de {MessageMaxLength} caractères.");
        }

        return Ok();
    }

    // ── Comment ────────────────────────────────────────────────────────────────

    public static ValidationResult ValidateComment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Fail("Le commentaire ne peut pas être vide.");
        }

        if (value.Length > CommentMaxLength)
        {
            return Fail($"Le commentaire dépasse la limite de {CommentMaxLength} caractères.");
        }

        return Ok();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ValidationResult Ok()
    {
        return new ValidationResult(true, null);
    }

    private static ValidationResult Fail(string msg)
    {
        return new ValidationResult(false, msg);
    }
}

/// <summary>Résultat d'une validation : succès ou message d'erreur localisé.</summary>
public readonly record struct ValidationResult(bool IsValid, string? ErrorMessage)
{
    public static implicit operator bool(ValidationResult r)
    {
        return r.IsValid;
    }
}