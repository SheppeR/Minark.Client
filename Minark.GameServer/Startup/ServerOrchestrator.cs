using System.Diagnostics;
using Minark.GameServer.Utils;

namespace Minark.GameServer.Startup;

/// <summary>
///     Orchestrateur de démarrage du serveur.
///     Exécute chaque <see cref="IStartupStep" /> dans l'ordre croissant de leur
///     propriété <see cref="IStartupStep.Order" /> avant de signaler la disponibilité
///     via <see cref="ServerReadySignal" />.
/// </summary>
/// <remarks>
///     Enregistré comme premier <c>IHostedService</c> dans <c>Program.cs</c>.
///     Les autres services (ex : <see cref="TickService" />) attendent
///     <see cref="ServerReadySignal.WaitAsync" /> avant de démarrer leur boucle.
/// </remarks>
public class ServerOrchestrator(
    IEnumerable<IStartupStep> steps,
    ServerReadySignal readySignal,
    Stopwatch sw,
    ILogger<ServerOrchestrator> log) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var ordered = steps.OrderBy(s => s.Order).ToList();
        log.LogInformation("Démarrage de {Count} étape(s) d'initialisation...", ordered.Count);

        foreach (var step in ordered)
        {
            var sw2 = Stopwatch.StartNew();

            try
            {
                await step.ExecuteAsync(ct);
            }
            catch (Exception ex)
            {
                log.LogCritical(ex, "Étape [{Step}] échouée — arrêt du serveur.", step.Name);
                throw; // remonte pour que l'hôte s'arrête proprement
            }

            log.LogInformation("[{Step}] prêt en {Ms} ms.", step.Name, sw2.ElapsedMilliseconds);
        }

        SerilogUtils.PrintSection("SERVER READY");

        sw.Stop();
        log.LogInformation("Server started in {Elapsed} ms ({Seconds:0.00}s)",
            sw.ElapsedMilliseconds,
            sw.Elapsed.TotalSeconds);

        // Débloque tous les services qui attendent (ex : TickService)
        readySignal.SetReady();

        SerilogUtils.PrintSection("SERVICES INFOS");
    }

    public Task StopAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}