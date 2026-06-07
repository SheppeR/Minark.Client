using System.Collections.Concurrent;
using Minark.Server.Services.Interfaces;

namespace Minark.Server.Services;

public class SessionStore : ISessionStore
{
    private readonly ConcurrentDictionary<int, Guid> _byUserId = new();
    private readonly ConcurrentDictionary<string, Guid> _byUsername = new(); // ✅ Index par username
    private readonly ConcurrentDictionary<Guid, (int UserId, string Username)> _clients = new();

    public Guid? SetUserForClient(Guid clientGuid, int userId, string username)
    {
        Guid? evictedGuid = null;

        // Cas 1 : le même user était déjà loggé depuis un autre GUID → on évince l'ancien.
        // IMPORTANT : on ne fait PAS RemoveClient(oldGuid) directement ici, car ça serait
        // fait plus tard par l'appelant après notification du client + fermeture socket.
        if (_byUserId.TryGetValue(userId, out var oldGuidForUser) && oldGuidForUser != clientGuid)
        {
            evictedGuid = oldGuidForUser;
            // Nettoyer les mappings de cette ancienne session pour qu'aucun message
            // ne soit routé vers elle entre temps.
            if (_clients.TryRemove(oldGuidForUser, out var oldUser))
            {
                _byUsername.TryRemove(oldUser.Username, out _);
            }
            // _byUserId[userId] sera écrasé plus bas — pas besoin de le TryRemove.
        }

        // Cas 2 : le même GUID est réutilisé pour un user différent (ex : logout + login
        // avec un autre compte sans refermer la socket). On purge les mappings de l'ancien
        // user attaché à ce GUID, sinon ils continuent de pointer vers notre GUID.
        if (_clients.TryGetValue(clientGuid, out var previous) &&
            (previous.UserId != userId || previous.Username != username))
        {
            _byUserId.TryRemove(previous.UserId, out _);
            _byUsername.TryRemove(previous.Username, out _);
        }

        _clients[clientGuid] = (userId, username);
        _byUserId[userId] = clientGuid;
        _byUsername[username] = clientGuid;

        return evictedGuid;
    }

    public void RemoveClient(Guid clientGuid)
    {
        if (_clients.TryRemove(clientGuid, out var user))
        {
            _byUserId.TryRemove(user.UserId, out _);
            _byUsername.TryRemove(user.Username, out _); // ✅ Nettoyer l'index
        }
    }

    public (int UserId, string Username)? GetUser(Guid clientGuid)
    {
        return _clients.TryGetValue(clientGuid, out var user) ? user : null;
    }

    public IEnumerable<Guid> GetOnlineClients()
    {
        return _clients.Keys;
    }

    // O(1) au lieu de O(n) — lookup direct dans l'index inverse
    public Guid? FindClientByUserId(int userId)
    {
        return _byUserId.TryGetValue(userId, out var guid) ? guid : null;
    }

    // ✅ Nouvelle méthode O(1) pour recherche par username
    public (Guid ClientGuid, int UserId, string Username)? GetClientByUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        if (_byUsername.TryGetValue(username, out var guid) &&
            _clients.TryGetValue(guid, out var user))
        {
            return (guid, user.UserId, user.Username);
        }

        return null;
    }
}