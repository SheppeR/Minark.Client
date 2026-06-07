namespace Minark.GameServer.Startup;

/// <summary>
///     Représente une étape de démarrage ordonnée du serveur.
///     Chaque étape est exécutée séquentiellement par le <see cref="ServerOrchestrator" />.
/// </summary>
public interface IStartupStep
{
    /// <summary>Nom affiché dans les logs lors de l'exécution.</summary>
    string Name { get; }

    /// <summary>
    ///     Ordre d'exécution (croissant). Utiliser les constantes de <see cref="StartupOrder" />.
    /// </summary>
    int Order { get; }

    Task ExecuteAsync(CancellationToken ct);
}

/// <summary>
///     Constantes d'ordre de démarrage.
/// </summary>
public static class StartupOrder
{
    public const int Database = 10;
    public const int Network = 20;

    public const int Services = 30;
    // Réservez 40, 50… pour de futures étapes (cache, API interne, etc.)
}