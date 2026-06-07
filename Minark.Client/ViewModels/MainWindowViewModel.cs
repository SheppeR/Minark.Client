using System.Windows;
using Minark.Client.Services;
using ReactiveUI;

namespace Minark.Client.ViewModels;

/// <summary>
///     ViewModel de la MainWindow : expose uniquement l'état offline.
///     La logique de navigation et d'animation reste en code-behind car elle
///     manipule directement des contrôles WPF (ContentControl, Transform).
/// </summary>
public class MainWindowViewModel : ReactiveObject
{
    public MainWindowViewModel(ReconnectionService reconnection)
    {
        reconnection.IsOfflineChanged += isOffline =>
            Application.Current?.Dispatcher.Invoke(() => IsOffline = isOffline);
    }

    public bool IsOffline
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }
}