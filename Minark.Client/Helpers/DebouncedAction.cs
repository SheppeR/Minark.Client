using System.Windows.Threading;

namespace Minark.Client.Helpers;

/// <summary>
///     Effectue une action avec debounce — n'exécute l'action que si aucun appel
///     n'intervient pendant le délai spécifié.
///     Utile pour réduire le nombre d'appels réseau lors d'événements fréquents (typing, search…)
/// </summary>
public class DebouncedAction : IDisposable
{
    private readonly Action _action;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public DebouncedAction(Action action, int delayMs = 300)
    {
        if (delayMs < 0)
        {
            throw new ArgumentException("Le délai ne peut pas être négatif", nameof(delayMs));
        }

        _action = action ?? throw new ArgumentNullException(nameof(action));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
        _timer.Tick += OnTick;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _disposed = true;
    }

    /// <summary>
    ///     Déclenche le debounce. L'action sera exécutée après le délai
    ///     si aucun nouvel appel n'intervient.
    /// </summary>
    public void Invoke()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>Annule l'exécution si elle n'a pas encore eu lieu.</summary>
    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (!_disposed)
        {
            _action.Invoke();
        }
    }
}

/// <summary>Version générique de DebouncedAction avec retour de valeur.</summary>
public class DebouncedFunc<T> : IDisposable
{
    private readonly Func<T> _func;
    private readonly DispatcherTimer _timer;
    private bool _disposed;
    private Action<T>? _pendingCallback;

    public DebouncedFunc(Func<T> func, int delayMs = 300)
    {
        if (delayMs < 0)
        {
            throw new ArgumentException("Le délai ne peut pas être négatif", nameof(delayMs));
        }

        _func = func ?? throw new ArgumentNullException(nameof(func));

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delayMs) };
        _timer.Tick += OnTick;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTick;
        _pendingCallback = null;
        _disposed = true;
    }

    public void Invoke(Action<T> callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _pendingCallback = callback;
        _timer.Stop();
        _timer.Start();
    }

    public void Stop()
    {
        _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (_disposed || _pendingCallback is null)
        {
            return;
        }

        try
        {
            var result = _func.Invoke();
            _pendingCallback(result);
        }
        catch
        {
            /* log silencieusement */
        }
    }
}