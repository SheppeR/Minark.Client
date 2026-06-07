using System.Windows;
using iNKORE.UI.WPF.Modern;

namespace Minark.Client.Services;

/// <summary>
///     Service for managing application theme (Dark/Light).
///     Pattern : on charge un dictionnaire de palette dédié (Colors.Dark.xaml ou
///     Colors.Light.xaml) en tête des MergedDictionaries de l'application. Tous
///     les contrôles consomment ces ressources via {DynamicResource …}, donc le
///     swap se propage instantanément. On synchronise aussi iNKORE (qui a son
///     propre ApplicationTheme) pour que les contrôles WinUI suivent.
/// </summary>
public static class ThemeService
{
    public enum AppTheme
    {
        Dark,
        Light
    }

    private const string DarkPaletteUri = "/Minark.Client;component/Themes/Colors.Dark.xaml";
    private const string LightPaletteUri = "/Minark.Client;component/Themes/Colors.Light.xaml";

    private static ResourceDictionary? _currentPalette;

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public static event Action<AppTheme>? ThemeChanged;

    /// <summary>
    ///     Change the application theme.
    ///     Idempotent : si le thème est déjà actif, ne fait rien.
    /// </summary>
    public static void SetTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var paletteUri = theme == AppTheme.Light ? LightPaletteUri : DarkPaletteUri;
        var newPalette = new ResourceDictionary
        {
            Source = new Uri(paletteUri, UriKind.Relative)
        };

        var merged = app.Resources.MergedDictionaries;
        var oldPalette = _currentPalette;

        // ORDRE CRITIQUE : on INSÈRE la nouvelle palette AVANT de retirer l'ancienne.
        // Pendant un court instant les deux coexistent — la nouvelle (en tête) prime
        // grâce à l'ordre de recherche WPF — puis on retire la précédente.
        // Inverser cet ordre (Remove puis Insert) provoque un instant pendant lequel
        // les clés thémables n'existent plus : DynamicResource lève alors une
        // InvalidOperationException si la propriété cible attend un type spécifique
        // (ex. BorderBrush) et que la résolution tombe sur un fallback string.
        merged.Insert(0, newPalette);
        _currentPalette = newPalette;

        if (oldPalette is not null)
        {
            merged.Remove(oldPalette);
        }

        CurrentTheme = theme;

        // Synchronise iNKORE pour les contrôles WinUI (TextBox, ComboBox, ToggleSwitch,
        // NavigationView, …). Sans ça, ces contrôles gardent leur thème compilé en dur.
        try
        {
            ThemeManager.Current.ApplicationTheme =
                theme == AppTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark;
        }
        catch
        {
            // ThemeManager peut ne pas être prêt au tout premier appel ; on ignore.
        }

        ThemeChanged?.Invoke(theme);
    }

    /// <summary>
    ///     Initialize theme from saved preference (à appeler au démarrage de l'app,
    ///     APRÈS que les MergedDictionaries de App.xaml soient chargés).
    /// </summary>
    public static void Initialize(AppTheme? savedTheme = null)
    {
        SetTheme(savedTheme ?? AppTheme.Dark);
    }
}