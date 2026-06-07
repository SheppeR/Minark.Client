using LiteNetLib;
using Microsoft.EntityFrameworkCore;
using Minark.GameServer.Data;
using Minark.GameServer.Network;
using Minark.GameServer.Services;

namespace Minark.GameServer.Handlers;

public class AuthHandler(
    IDbContextFactory<GameDbContext> dbFactory,
    PlayerRegistry registry,
    IServerSender sender,
    MinarServerStatusClient statusClient,
    ILogger<AuthHandler> log) : IPacketHandler
{
    public GamePacketType PacketType => GamePacketType.AuthRequest;

    public async Task HandleAsync(NetPeer peer, GamePacket packet, CancellationToken ct)
    {
        var req = GamePacketSerializer.DeserializePayload<AuthRequest>(packet.Payload);
        if (req is null || string.IsNullOrWhiteSpace(req.Token))
        {
            sender.Send(peer, GamePacketType.AuthResponse,
                new AuthResponse { Success = false, Error = "Token manquant." });
            return;
        }

        // ── Validation token en DB ────────────────────────────────────────────
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var session = await db.Sessions
            .Include(s => s.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Token == req.Token && s.ExpiresAt > DateTime.UtcNow, ct);

        if (session is null)
        {
            log.LogWarning("Auth échouée — token invalide/expiré (peer {PeerId})", peer.Id);
            sender.Send(peer, GamePacketType.AuthResponse,
                new AuthResponse { Success = false, Error = "Token invalide ou expiré." });
            return;
        }

        // ── Double connexion ──────────────────────────────────────────────────
        if (registry.IsUserConnected(session.UserId))
        {
            log.LogWarning("Auth refusée — user {UserId} déjà connecté", session.UserId);
            sender.Send(peer, GamePacketType.AuthResponse,
                new AuthResponse { Success = false, Error = "Déjà connecté depuis un autre client." });
            return;
        }

        // ── Chargement des amis ───────────────────────────────────────────────
        var friendIds = await db.Friendships
            .AsNoTracking()
            .Where(f => f.Status == FriendshipStatus.Accepted &&
                        (f.RequesterId == session.UserId || f.AddresseeId == session.UserId))
            .Select(f => f.RequesterId == session.UserId ? f.AddresseeId : f.RequesterId)
            .ToHashSetAsync(ct);

        // ── Enregistrement ────────────────────────────────────────────────────
        var player = new ConnectedPlayer
        {
            UserId = session.UserId,
            Username = session.User.Username,
            AvatarUrl = session.User.AvatarUrl,
            Token = req.Token,
            PreviousStatusInt = session.User.Status, // ← int brut depuis la DB
            Peer = peer,
            FriendIds = friendIds
        };

        registry.TryAdd(player);
        log.LogInformation("Joueur connecté : {Username} (id={UserId}, peer={PeerId}, previousStatus={PreviousStatus})",
            player.Username, player.UserId, peer.Id, player.PreviousStatusInt);

        // ── Réponse auth OK ───────────────────────────────────────────────────
        sender.Send(peer, GamePacketType.AuthResponse, new AuthResponse
        {
            Success = true,
            User = new GameUserDto
            {
                Id = player.UserId,
                Username = player.Username,
                AvatarUrl = player.AvatarUrl,
                IsAdmin = session.User.IsAdmin
            }
        });

        // ── Notifier les amis connectés au GameServer (UDP) ───────────────────
        var statusUpdate = new FriendStatusUpdate
        {
            FriendId = player.UserId,
            Status = GameUserStatus.InGame
        };
        foreach (var friend in registry.FriendsOf(player))
        {
            sender.Send(friend.Peer, GamePacketType.FriendStatusUpdate, statusUpdate);
        }

        // ── Notifier le Minark.Server (HTTP) → amis connectés au launcher ─────
        _ = statusClient.SetInGameAsync(req.Token);
    }
}