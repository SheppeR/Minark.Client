using LiteNetLib;
using Minark.GameServer.Network;
using Minark.GameServer.Services;

namespace Minark.GameServer;

/// <summary>
///     Boucle de tick serveur.
///     À chaque tick (configurable, défaut 20 Hz) :
///     - Poll LiteNetLib (obligatoire)
///     - Broadcast positions de tous les joueurs (Unreliable)
/// </summary>
public class TickService(
    NetManager netManager,
    PlayerRegistry registry,
    IServerSender sender,
    GameServerOptions opts,
    ILogger<TickService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var intervalMs = 1000 / Math.Max(1, opts.TickRateHz);
        log.LogInformation("Tick loop démarrée @ {Hz} Hz (interval={Ms}ms)", opts.TickRateHz, intervalMs);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            // ── Poll LiteNetLib (OBLIGATOIRE dans le thread principal) ────────
            netManager.PollEvents();

            // ── Broadcast positions ───────────────────────────────────────────
            BroadcastPositions();
        }
    }

    private void BroadcastPositions()
    {
        var players = registry.All.ToArray();
        if (players.Length == 0)
        {
            return;
        }

        // Construire le snapshot global
        var snapshots = players.Select(p => new PlayerSnapshot
        {
            PlayerId = p.UserId,
            X = p.X,
            Y = p.Y,
            Z = p.Z,
            Rot = p.Rot,
            Tick = p.LastMoveTick
        }).ToArray();

        var syncPacket = new PlayerMoveSyncPacket { Players = snapshots };

        // Envoyer à chaque joueur connecté (Unreliable — perte acceptable)
        foreach (var player in players)
        {
            sender.Send(player.Peer, GamePacketType.PlayerMoveSync, syncPacket,
                DeliveryMethod.Unreliable);
        }
    }
}