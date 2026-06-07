using Minark.Server.Services.Interfaces;
using Minark.Server.Startup;

namespace Minark.Server.Infrastructure;

/// <summary>
///     Purge périodique des anciens messages de chat.
///     Attend <see cref="ServerReadySignal" /> avant de démarrer (DB garantie initialisée).
/// </summary>
public class MessagePurgeService(
    IServiceScopeFactory scopeFactory,
    ServerReadySignal readySignal,
    ILogger<MessagePurgeService> logger,
    IConfiguration config)
    : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromHours(config.GetValue("MessagePurge:IntervalHours", 1));
    private readonly int _retentionDays = config.GetValue("MessagePurge:RetentionDays", 30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Attendre que la DB et le réseau soient prêts
        await readySignal.WaitAsync(stoppingToken);

        logger.LogInformation(
            "MessagePurgeService démarré — toutes les {H}h, rétention {D} jours",
            _interval.TotalHours, _retentionDays);

        // Purge initiale au démarrage
        await RunPurgeAsync(stoppingToken);

        using var timer = new PeriodicTimer(_interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await RunPurgeAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erreur lors de la purge — nouvelle tentative au prochain tick");
            }
        }

        logger.LogInformation("MessagePurgeService arrêté.");
    }

    private async Task RunPurgeAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
            await chat.PurgeOldMessagesAsync(_retentionDays);
            logger.LogInformation("Purge des messages terminée (rétention : {D} jours)", _retentionDays);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Échec de la purge des messages");
        }
    }
}