using LiteNetLib;
using Minark.GameServer.Network;
using Minark.GameServer.Services;
using Minark.GameServer.Startup;

namespace Minark.GameServer;

/// <summary>
///     Boucle de tick serveur — démarre uniquement après que
///     <see cref="ServerOrchestrator" /> a signalé la disponibilité complète.
/// </summary>
public class TickService(
    NetManager netManager,
    PlayerRegistry registry,
    IServerSender sender,
    GameServerOptions opts,
    ServerReadySignal readySignal,
    ILogger<TickService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // ── Attendre que DB + réseau soient prêts ─────────────────────────────
        await readySignal.WaitAsync(ct);

        var intervalMs = 1000 / Math.Max(1, opts.TickRateHz);

        log.LogInformation("Tick loop démarrée @ {Hz} Hz (interval={Ms} ms)", opts.TickRateHz, intervalMs);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

        while (await timer.WaitForNextTickAsync(ct))
        {
            netManager.PollEvents();
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

        foreach (var player in players)
        {
            sender.Send(player.Peer, GamePacketType.PlayerMoveSync, syncPacket,
                DeliveryMethod.Unreliable);
        }
    }
}