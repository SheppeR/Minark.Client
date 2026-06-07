using LiteNetLib;
using Minark.GameServer.Handlers;
using Minark.GameServer.Network;
using Minark.GameServer.Services;

namespace Minark.GameServer;

/// <summary>
///     Hosted service qui démarre le listener LiteNetLib et gère les événements réseau.
/// </summary>
public class LiteNetHostedService(
    NetManager netManager,
    EventBasedNetListener listener,
    PacketDispatcher dispatcher,
    PlayerRegistry registry,
    IServerSender sender,
    GameServerOptions opts,
    ILogger<LiteNetHostedService> log) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        // ── Événements LiteNetLib ─────────────────────────────────────────────

        listener.ConnectionRequestEvent += request =>
        {
            if (netManager.ConnectedPeersCount < opts.MaxPlayers)
            {
                request.AcceptIfKey(opts.ConnectionKey);
            }
            else
            {
                request.Reject();
            }
        };

        listener.PeerConnectedEvent += peer => { log.LogDebug("Peer connecté : {PeerId} ({Endpoint})", peer.Id, peer.Address); };

        listener.PeerDisconnectedEvent += (peer, info) =>
        {
            if (!registry.TryRemove(peer.Id, out var player) || player is null)
            {
                return;
            }

            log.LogInformation("Joueur déconnecté : {Username} (reason={Reason})",
                player.Username, info.Reason);

            // Notifier les amis en jeu
            var statusUpdate = new FriendStatusUpdate
            {
                FriendId = player.UserId,
                Status = GameUserStatus.Offline
            };
            foreach (var friend in registry.FriendsOf(player))
            {
                sender.Send(friend.Peer, GamePacketType.FriendStatusUpdate, statusUpdate);
            }
        };

        listener.NetworkReceiveEvent += async (peer, reader, channel, delivery) =>
        {
            var data = reader.GetRemainingBytes();
            reader.Recycle(); // recycle manuel AVANT le await
            await dispatcher.DispatchAsync(peer, data, 0, data.Length, CancellationToken.None);
        };

        // ── Démarrage ─────────────────────────────────────────────────────────
        var started = netManager.Start(opts.Port);
        if (!started)
        {
            throw new InvalidOperationException($"Impossible de démarrer LiteNetLib sur le port {opts.Port}");
        }

        log.LogInformation("GameServer LiteNetLib démarré sur 0.0.0.0:{Port}", opts.Port);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        log.LogInformation("Arrêt du GameServer...");
        netManager.Stop();
        return Task.CompletedTask;
    }
}