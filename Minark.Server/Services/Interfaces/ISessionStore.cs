namespace Minark.Server.Services.Interfaces;

/// <summary>
///     Tracks active TCP connections and their associated authenticated users.
///     OPTIMISATION : ajout d'un index inverse userId→clientGuid.
///     AVANT : FindClientByUserId() parcourait tout le dictionnaire O(n) à chaque message envoyé.
///     Sur 1000 clients connectés = 1000 comparaisons pour trouver le destinataire.
///     APRÈS : lookup O(1) via _byUserId.
/// </summary>
public interface ISessionStore
{
    /// <summary>
    ///     Associe <paramref name="clientGuid" /> à l'utilisateur authentifié.
    ///     Si le même utilisateur était déjà connecté depuis un autre GUID, celui-ci est
    ///     expulsé et retourné. L'appelant doit alors envoyer une notification
    ///     <see cref="PacketType.SessionInvalidated" /> puis fermer la socket correspondante.
    /// </summary>
    /// <returns>L'ancien GUID évincé si une double session a été détectée, <c>null</c> sinon.</returns>
    Guid? SetUserForClient(Guid clientGuid, int userId, string username);

    void RemoveClient(Guid clientGuid);
    (int UserId, string Username)? GetUser(Guid clientGuid);
    IEnumerable<Guid> GetOnlineClients();
    Guid? FindClientByUserId(int userId);
    (Guid ClientGuid, int UserId, string Username)? GetClientByUsername(string username);
}