using LiteNetLib;
using Minark.GameServer.Handlers;
using Minark.GameServer.Network;
using Minark.GameServer.Services;
using Minark.GameServer.Utils;

namespace Minark.GameServer.Startup;

/// <summary>
///     Étape 2 — Réseau LiteNetLib.
///     Enregistre les événements et bind le port UDP.
/// </summary>
public class NetworkStartupStep(
    NetManager netManager,
    EventBasedNetListener listener,
    PacketDispatcher dispatcher,
    PlayerRegistry registry,
    IServerSender sender,
    MinarServerStatusClient statusClient,
    GameServerOptions opts,
    ILogger<NetworkStartupStep> log) : IStartupStep
{
    public string Name => "Network";
    public int Order => StartupOrder.Network;

    public Task ExecuteAsync(CancellationToken ct)
    {
        SerilogUtils.PrintSection("NETWORK");

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

        listener.PeerConnectedEvent += peer =>
            log.LogDebug("Peer connecté : {PeerId} ({Endpoint})", peer.Id, peer.Address);

        listener.PeerDisconnectedEvent += (peer, info) =>
        {
            if (!registry.TryRemove(peer.Id, out var player) || player is null)
            {
                return;
            }

            log.LogInformation("Joueur déconnecté : {Username} (reason={Reason}, restoringStatus={Status})",
                player.Username, info.Reason, player.PreviousStatusInt);

            // ── Notifier les amis connectés au GameServer (UDP) ───────────────
            var statusUpdate = new FriendStatusUpdate
            {
                FriendId = player.UserId,
                Status = GameUserStatus.Offline
            };
            foreach (var friend in registry.FriendsOf(player))
            {
                sender.Send(friend.Peer, GamePacketType.FriendStatusUpdate, statusUpdate);
            }

            // ── Restaurer le statut précédent via Minark.Server (HTTP) ────────
            _ = statusClient.RestoreStatusAsync(player.Token, player.PreviousStatusInt);
        };

        listener.NetworkReceiveEvent += async (peer, reader, channel, delivery) =>
        {
            var data = reader.GetRemainingBytes();
            reader.Recycle();
            await dispatcher.DispatchAsync(peer, data, 0, data.Length, CancellationToken.None);
        };

        // ── Bind ──────────────────────────────────────────────────────────────
        var started = netManager.Start(opts.Port);
        if (!started)
        {
            throw new InvalidOperationException(
                $"Impossible de démarrer LiteNetLib sur le port {opts.Port}.");
        }

        log.LogInformation("LiteNetLib démarré sur 0.0.0.0:{Port}", opts.Port);
        return Task.CompletedTask;
    }
}