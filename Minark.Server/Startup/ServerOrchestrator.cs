using System.Diagnostics;
using Minark.Server.Infrastructure;
using Minark.Shared;

namespace Minark.Server.Startup;

/// <summary>
///     Orchestrateur de démarrage du serveur.
///     Exécute chaque <see cref="IStartupStep" /> dans l'ordre croissant de leur
///     propriété <see cref="IStartupStep.Order" />, puis lève <see cref="ServerReadySignal" />
///     pour débloquer les services dépendants (ex : <see cref="MessagePurgeService" />).
/// </summary>
/// <remarks>
///     Doit être le PREMIER <c>IHostedService</c> enregistré dans <c>Program.cs</c>.
/// </remarks>
public class ServerOrchestrator(
    IEnumerable<IStartupStep> steps,
    ServerReadySignal readySignal,
    Stopwatch sw,
    ILogger<ServerOrchestrator> log) : IHostedService
{
    private NetworkStartupStep? _networkStep;

    public async Task StartAsync(CancellationToken ct)
    {
        var ordered = steps.OrderBy(s => s.Order).ToList();
        log.LogInformation("Démarrage de {Count} étape(s) d'initialisation...", ordered.Count);

        foreach (var step in ordered)
        {
            var timer = Stopwatch.StartNew();

            try
            {
                await step.ExecuteAsync(ct);
            }
            catch (Exception ex)
            {
                log.LogCritical(ex, "Étape [{Step}] échouée — arrêt du serveur.", step.Name);
                throw;
            }

            log.LogInformation("[{Step}] prêt en {Ms} ms.", step.Name, timer.ElapsedMilliseconds);

            // Garder une référence au NetworkStep pour l'arrêt propre
            if (step is NetworkStartupStep ns)
            {
                _networkStep = ns;
            }
        }

        SerilogUtils.PrintSection("SERVER READY");
        sw.Stop();
        log.LogInformation("Server started in {Elapsed} ms ({Seconds:0.00}s)",
            sw.ElapsedMilliseconds,
            sw.Elapsed.TotalSeconds);

        SerilogUtils.PrintSection("SERVICES INFOS");

        // Débloque tous les BackgroundService qui attendent
        readySignal.SetReady();
    }

    public async Task StopAsync(CancellationToken ct)
    {
        SerilogUtils.PrintSection("SERVER STOPPING");
        if (_networkStep is not null)
        {
            await _networkStep.StopAsync(ct);
        }
    }
}