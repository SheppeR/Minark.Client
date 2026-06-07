using Microsoft.EntityFrameworkCore;
using Minark.Server.Data;

namespace Minark.Server.Infrastructure;

public class DatabaseInitializer(AppDbContext db, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying pending migrations...");

        await EnsureConsistentMigrationStateAsync(cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Schema up to date.");

        await db.Users.ExecuteUpdateAsync(
            s => s.SetProperty(u => u.Status, 0), cancellationToken);
        await db.Sessions.ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Database ready — all users set offline, sessions cleared.");
    }

    /// <summary>
    ///     Détecte un __EFMigrationsHistory périmé :
    ///     migrations enregistrées comme appliquées, mais schéma absent.
    ///     Cela arrive lors de la transition depuis l'ancienne gestion SQL brut,
    ///     ou après une réinitialisation partielle de la base.
    ///     Dans ce cas on vide l'historique pour que MigrateAsync rejoue tout proprement.
    /// </summary>
    private async Task EnsureConsistentMigrationStateAsync(CancellationToken cancellationToken)
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();

        // Aucun historique → base neuve, MigrateAsync s'en charge normalement.
        if (applied.Count == 0)
        {
            return;
        }

        // Historique présent : vérifier que le schéma existe réellement.
        var schemaIntact = await SchemaExistsAsync(cancellationToken);
        if (schemaIntact)
        {
            return;
        }

        // Historique périmé : on le vide pour forcer une ré-application complète.
        logger.LogWarning(
            "Migration history present ({Count} entr\u00e9e(s)) but schema is missing — " +
            "clearing history to force a clean re-apply.", applied.Count);

        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM `__EFMigrationsHistory`", cancellationToken);
    }

    private async Task<bool> SchemaExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.Users.AnyAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}