using Microsoft.EntityFrameworkCore;
using Minark.GameServer.Data;
using Minark.GameServer.Utils;

namespace Minark.GameServer.Startup;

/// <summary>
///     Étape 1 — Base de données.
///     Vérifie la connectivité MariaDB et applique les migrations en attente.
/// </summary>
public class DatabaseStartupStep(
    IDbContextFactory<GameDbContext> dbFactory,
    ILogger<DatabaseStartupStep> log) : IStartupStep
{
    public string Name => "Database";
    public int Order => StartupOrder.Database;

    public async Task ExecuteAsync(CancellationToken ct)
    {
        SerilogUtils.PrintSection("DATABASE");
        log.LogInformation("Vérification de la connexion MariaDB...");

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Ping
        var canConnect = await db.Database.CanConnectAsync(ct);
        if (!canConnect)
        {
            throw new InvalidOperationException(
                "Impossible de se connecter à la base de données. Vérifiez la chaîne de connexion.");
        }

        // Migrations
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            log.LogInformation("Application de {Count} migration(s) : {Migrations}",
                pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync(ct);
        }

        log.LogInformation("Base de données OK (0 migration en attente).");
    }
}