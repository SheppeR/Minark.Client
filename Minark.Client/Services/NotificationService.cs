using System.Windows;
using Minark.Client.Helpers;
using Minark.Client.Views.Shared;

namespace Minark.Client.Services;

/// <summary>
///     Affiche une notification à l'utilisateur.
///     - Si la fenêtre principale est visible et active  → Toast WPF custom (ToastWindow).
///     - Si l'app est en arrière-plan / minimisée        → Balloon tray (via delegate injecté par App).
/// </summary>
public class NotificationService
{
    /// <summary>
    ///     Action injectée par App.xaml.cs après la création du TaskbarIcon.
    ///     Appelée avec (titre, message) pour afficher un balloon tray.
    ///     Si null, on retombe sur ToastWindow même en arrière-plan.
    /// </summary>
    public Action<string, string>? ShowTrayBalloon { get; set; }

    public bool ToastEnabled { get; set; } = true;

    public void ShowMessage(string from, string body)
    {
        if (!ToastEnabled)
        {
            return;
        }

        UiThread.Invoke(() =>
        {
            var preview = body.Length > 80 ? body[..80] + "…" : body;

            if (!IsMainWindowFocused() && ShowTrayBalloon is not null)
            {
                // App en arrière-plan → balloon tray
                ShowTrayBalloon(from, preview);
            }
            else
            {
                // Fenêtre au premier plan (ou pas de tray dispo) → toast in-app
                new ToastWindow(from, preview).Show();
            }
        });
    }

    private static bool IsMainWindowFocused()
    {
        var win = Application.Current?.MainWindow;
        return win is { IsVisible: true, IsActive: true };
    }
}