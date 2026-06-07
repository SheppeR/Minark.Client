using Minark.Server.Services.Interfaces;

namespace Minark.Server.Infrastructure;

/// <summary>
///     Purge périodique des anciens messages de chat.
///     Avant : appelé une seule fois au démarrage — les messages s'accumulaient entre redémarrages.
///     Après : PeriodicTimer qui tourne toutes les N heures (configurable via appsettings.json).
/// </summary>
public class MessagePurgeService(
    IServiceScopeFactory scopeFactory,
    ILogger<MessagePurgeService> logger,
    IConfiguration config)
    : BackgroundService
{
    private readonly TimeSpan _interval = TimeSpan.FromHours(config.GetValue("MessagePurge:IntervalHours", 1));
    private readonly int _retentionDays = config.GetValue("MessagePurge:RetentionDays", 30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "MessagePurgeService started — every {H}h, retention {D} days",
            _interval.TotalHours, _retentionDays);

        // Purge initiale au démarrage (remplace l'appel dans TcpServerHostedService)
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
                logger.LogError(ex, "Error during message purge — will retry next tick");
            }
        }

        logger.LogInformation("MessagePurgeService stopped.");
    }

    private async Task RunPurgeAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var chat = scope.ServiceProvider.GetRequiredService<IChatService>();
            await chat.PurgeOldMessagesAsync(_retentionDays);
            logger.LogInformation("Message purge completed (retention: {D} days)", _retentionDays);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Message purge failed");
        }
    }
}