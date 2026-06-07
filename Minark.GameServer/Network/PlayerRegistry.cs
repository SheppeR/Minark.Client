using System.Collections.Concurrent;

namespace Minark.GameServer.Network;

/// <summary>
///     Registre thread-safe des joueurs connectés.
///     Clé : NetPeer.Id (int assigné par LiteNetLib).
/// </summary>
public class PlayerRegistry
{
    private readonly ConcurrentDictionary<int, ConnectedPlayer> _byPeerId = new();

    private readonly ConcurrentDictionary<int, ConnectedPlayer> _byUserId = new();

    // ── Itération ─────────────────────────────────────────────────────────────

    public IEnumerable<ConnectedPlayer> All => _byPeerId.Values;

    public int Count => _byPeerId.Count;

    // ── Enregistrement ────────────────────────────────────────────────────────

    public bool TryAdd(ConnectedPlayer player)
    {
        if (!_byPeerId.TryAdd(player.Peer.Id, player))
        {
            return false;
        }

        _byUserId[player.UserId] = player;
        return true;
    }

    public bool TryRemove(int peerId, out ConnectedPlayer? player)
    {
        if (!_byPeerId.TryRemove(peerId, out player))
        {
            return false;
        }

        _byUserId.TryRemove(player.UserId, out _);
        return true;
    }

    // ── Lookups ───────────────────────────────────────────────────────────────

    public bool TryGetByPeer(int peerId, out ConnectedPlayer? player)
    {
        return _byPeerId.TryGetValue(peerId, out player);
    }

    public bool TryGetByUser(int userId, out ConnectedPlayer? player)
    {
        return _byUserId.TryGetValue(userId, out player);
    }

    public bool IsUserConnected(int userId)
    {
        return _byUserId.ContainsKey(userId);
    }

    public IEnumerable<ConnectedPlayer> FriendsOf(ConnectedPlayer player)
    {
        foreach (var friendId in player.FriendIds)
        {
            if (_byUserId.TryGetValue(friendId, out var friend))
            {
                yield return friend;
            }
        }
    }
}