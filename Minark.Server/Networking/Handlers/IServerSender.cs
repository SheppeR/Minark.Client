namespace Minark.Server.Networking.Handlers;

/// <summary>Abstraction d'envoi de paquets vers les clients connectés.</summary>
public interface IServerSender
{
    Task SendAsync<T>(Guid clientGuid, PacketType type, T payload);
    Task PushStatusToFriendsAsync(int userId, string username, UserStatus status);
    Task PushFriendListChangedAsync(int actorId, string actorUsername, string otherUsername, string reason);

    /// <summary>
    ///     Ferme la socket TCP d'un client donné. Typiquement utilisé après l'envoi
    ///     d'une notification <see cref="PacketType.SessionInvalidated" />.
    ///     Tolère silencieusement le cas où le GUID n'existe plus.
    /// </summary>
    Task DisconnectClientAsync(Guid clientGuid);
}