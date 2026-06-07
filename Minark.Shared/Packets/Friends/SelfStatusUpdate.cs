namespace Minark.Shared.Packets.Friends;

/// <summary>
///     Envoyé par le serveur au client pour lui notifier
///     son propre changement de statut (ex: InGame déclenché par le GameServer).
/// </summary>
public class SelfStatusUpdate
{
    public UserStatus Status { get; init; }
}