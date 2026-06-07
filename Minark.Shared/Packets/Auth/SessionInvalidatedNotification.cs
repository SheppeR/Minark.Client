namespace Minark.Shared.Packets.Auth;

/// <summary>
///     Envoyé par le serveur à un client dont la session a été invalidée
///     (typiquement : le même compte vient d'être loggé depuis un autre endroit).
///     Le client doit nettoyer son état local et revenir à l'écran de login.
/// </summary>
public class SessionInvalidatedNotification
{
    public string Reason { get; init; } = string.Empty;
}