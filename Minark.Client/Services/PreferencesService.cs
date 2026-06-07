using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace Minark.Client.Services;

/// <summary>
///     Préférences persistées par profil utilisateur.
///     Le fichier est stocké dans <c>ProfileService.DataDirectory/preferences.json</c>
///     et rechargé automatiquement après chaque basculement de profil (post-login).
/// </summary>
public class PreferencesService
{
    private const string FileName = "preferences.json";

    private readonly ProfileService _profile;
    private Dictionary<string, object> _prefs = new();

    public PreferencesService(ProfileService profile)
    {
        _profile = profile;
        Load();
        _profile.ProfileSwitched += Load;
    }

    // ── Thème ──────────────────────────────────────────────────────────────────

    public void SetTheme(ThemeService.AppTheme theme)
    {
        _prefs["Theme"] = theme.ToString();
        Save();
    }

    public ThemeService.AppTheme GetTheme()
    {
        if (_prefs.TryGetValue("Theme", out var v) && Enum.TryParse<ThemeService.AppTheme>(Str(v), out var t))
        {
            return t;
        }

        return ThemeService.AppTheme.Dark;
    }

    // ── Accent ─────────────────────────────────────────────────────────────────

    public void SetAccentColor(Color color)
    {
        _prefs["AccentColor"] = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        Save();
    }

    public Color? GetAccentColor()
    {
        if (!_prefs.TryGetValue("AccentColor", out var v))
        {
            return null;
        }

        try
        {
            var s = Str(v);
            if (!string.IsNullOrWhiteSpace(s))
            {
                return (Color)ColorConverter.ConvertFromString(s);
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    // ── Son ────────────────────────────────────────────────────────────────────

    public void SetSoundEnabled(bool value)
    {
        _prefs["SoundEnabled"] = value;
        Save();
    }

    public bool GetSoundEnabled()
    {
        return GetBool("SoundEnabled", true);
    }

    // ── Toast ──────────────────────────────────────────────────────────────────

    public void SetToastEnabled(bool value)
    {
        _prefs["ToastEnabled"] = value;
        Save();
    }

    public bool GetToastEnabled()
    {
        return GetBool("ToastEnabled", true);
    }

    // ── Game ───────────────────────────────────────────────────────────────────

    public void SetGameInstallPath(string path)
    {
        _prefs["GameInstallPath"] = path;
        Save();
    }

    public string? GetGameInstallPath()
    {
        return _prefs.TryGetValue("GameInstallPath", out var v) ? Str(v) : null;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void Load()
    {
        var path = _profile.GetFilePath(FileName);
        try
        {
            _prefs = File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path)) ??
                  new Dictionary<string, object>()
                : new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PreferencesService.Load: {ex.Message}");
            _prefs = new Dictionary<string, object>();
        }
    }

    private void Save()
    {
        var path = _profile.GetFilePath(FileName);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(_prefs));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PreferencesService.Save: {ex.Message}");
        }
    }

    private bool GetBool(string key, bool def)
    {
        if (!_prefs.TryGetValue(key, out var v))
        {
            return def;
        }

        return v switch
        {
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.False } => false,
            bool b => b,
            _ => bool.TryParse(v.ToString(), out var b2) ? b2 : def
        };
    }

    private static string? Str(object v)
    {
        return v switch
        {
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString(),
            string s => s,
            _ => v.ToString()
        };
    }
}