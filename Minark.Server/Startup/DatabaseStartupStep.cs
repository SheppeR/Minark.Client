using Minark.Server.Infrastructure;
using Minark.Shared;

namespace Minark.Server.Startup;

/// <summary>
///     Étape 1 — Base de données.
///     Applique les migrations EF, remet tous les users offline et purge les sessions.
/// </summary>
public class DatabaseStartupStep(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseStartupStep> log) : IStartupStep
{
    public string Name => "Database";
    public int Order => StartupOrder.Database;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        SerilogUtils.PrintSection("DATABASE");

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbInit = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await dbInit.InitializeAsync(ct);

        log.LogInformation("Base de données prête.");
    }
}