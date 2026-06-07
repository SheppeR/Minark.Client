namespace Minark.Server.Startup;

/// <summary>
///     Signal partagé (singleton) que l'orchestrateur lève quand toutes les
///     étapes de démarrage sont terminées.
///     Les services dépendants (ex : <see cref="MessagePurgeService"/>) l'attendent
///     via <see cref="WaitAsync"/> avant de commencer leur boucle.
/// </summary>
public sealed class ServerReadySignal
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Appelé par <see cref="ServerOrchestrator"/> quand tout est prêt.</summary>
    internal void SetReady() => _tcs.TrySetResult();

    /// <summary>Attend que le serveur soit prêt (annulable).</summary>
    public Task WaitAsync(CancellationToken ct = default)
    {
        ct.Register(() => _tcs.TrySetCanceled(ct));
        return _tcs.Task;
    }
}
