namespace Minark.Shared.Packets.Auth;

/// <summary>
///     Étape 2 du login.
///     Password transite en clair sur la connexion TLS.
///     Hmac = HMAC-SHA256(SHA256(password), nonce) pour défense en profondeur.
/// </summary>
public class LoginRequest
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Hmac { get; init; } = string.Empty;
}