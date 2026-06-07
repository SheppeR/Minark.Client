namespace Minark.Server.Http;

/// <summary>
///     Corps JSON envoyé par le GameServer au Minark.Server
///     via POST /internal/player-status.
/// </summary>
public class InternalStatusRequest
{
    /// <summary>Token de session du joueur (identique à celui du launcher).</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>Nouveau statut à appliquer (Online au logout, InGame au login).</summary>
    public UserStatus Status { get; init; }
}