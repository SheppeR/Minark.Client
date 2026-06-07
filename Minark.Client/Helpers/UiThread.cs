using System.Windows;

namespace Minark.Client.Helpers;

/// <summary>
///     Helpers pour dispatcher les actions sur le thread UI WPF.
///     Remplace les appels verbeux à Application.Current?.Dispatcher.Invoke(...)
///     partout dans les ViewModels et Services.
/// </summary>
public static class UiThread
{
    /// <summary>
    ///     Exécute une action synchrone sur le thread UI.
    ///     Équivalent à : Application.Current?.Dispatcher.Invoke(action)
    /// </summary>
    public static void Invoke(Action action)
    {
        Application.Current?.Dispatcher.Invoke(action);
    }

    /// <summary>
    ///     Poste une action sur le thread UI sans attendre (fire-and-forget).
    ///     Équivalent à : Application.Current?.Dispatcher.InvokeAsync(action)
    /// </summary>
    public static void Post(Action action)
    {
        Application.Current?.Dispatcher.InvokeAsync(action);
    }

    /// <summary>
    ///     Poste une tâche async sur le thread UI sans attendre.
    ///     Équivalent à : Application.Current?.Dispatcher.InvokeAsync(async () => await task())
    /// </summary>
    public static void Post(Func<Task> action)
    {
        Application.Current?.Dispatcher.InvokeAsync(action);
    }

    /// <summary>
    ///     Exécute une action async sur le thread UI et l'attend depuis un thread background.
    ///     Équivalent à : await Application.Current?.Dispatcher.InvokeAsync(action)
    /// </summary>
    public static Task InvokeAsync(Func<Task> action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task.Unwrap();
    }
}