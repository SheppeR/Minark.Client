using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Minark.Client.Services;

public class SavedCredentials
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}

public interface ICredentialsService
{
    /// <summary>Charge le dernier compte utilisé (celui qui a coché "Se souvenir" en dernier).</summary>
    SavedCredentials Load();

    /// <summary>Charge les credentials d'un compte spécifique. Retourne des credentials vides si absent.</summary>
    SavedCredentials Load(string username);

    /// <summary>Enumère les usernames sauvés sur cette machine.</summary>
    IReadOnlyList<string> GetSavedUsernames();

    /// <summary>Enregistre (ou met à jour) les credentials d'un compte et le définit comme dernier utilisé.</summary>
    void Save(string username, string password, bool rememberMe);

    /// <summary>Supprime les credentials du compte courant (ne touche pas aux autres comptes sauvés).</summary>
    void Clear();
}

/// <summary>
///     Gestion des credentials multi-comptes. Permet de mémoriser plusieurs utilisateurs
///     sur la même machine (utile pour tester plusieurs clients ou pour un PC partagé).
///     Les mots de passe sont chiffrés via DPAPI (clé liée à l'utilisateur Windows courant) :
///     le fichier n'est déchiffrable que par le même utilisateur sur la même machine.
/// </summary>
public class CredentialsService : ICredentialsService
{
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };
    private static readonly Lock _lock = new();

    // credentials.json est volontairement GLOBAL (hors des profils) : il contient la liste
    // des comptes sauvés sur la machine, ce qui alimente la liste déroulante au login
    // avant que l'utilisateur ait choisi un compte.
    private static readonly string _path = Path.Combine(ProfileService.RootDirectory, "credentials.json");

    public SavedCredentials Load()
    {
        var store = ReadStore();
        if (string.IsNullOrEmpty(store.LastUsername))
        {
            return new SavedCredentials();
        }

        return Load(store.LastUsername);
    }

    public SavedCredentials Load(string username)
    {
        var store = ReadStore();
        if (!store.Accounts.TryGetValue(username, out var entry))
        {
            return new SavedCredentials();
        }

        var password = TryDecrypt(entry.EncryptedPassword);
        return new SavedCredentials
        {
            Username = username,
            Password = password ?? string.Empty,
            RememberMe = password is not null
        };
    }

    public IReadOnlyList<string> GetSavedUsernames()
    {
        return ReadStore().Accounts.Keys.ToList();
    }

    public void Save(string username, string password, bool rememberMe)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        lock (_lock)
        {
            var store = ReadStore();

            if (rememberMe)
            {
                var encrypted = TryEncrypt(password);
                if (encrypted is null)
                {
                    return; // Si DPAPI échoue (plateforme non Windows par ex.), on abandonne la sauvegarde.
                }

                store.Accounts[username] = new AccountEntry { EncryptedPassword = encrypted };
            }
            else
            {
                // "Se souvenir" non coché : on retire ce compte de la persistance mais on le marque
                // comme dernier utilisé pour pré-remplir le champ username au prochain démarrage.
                store.Accounts.Remove(username);
            }

            store.LastUsername = username;
            WriteStore(store);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            var store = ReadStore();
            if (!string.IsNullOrEmpty(store.LastUsername))
            {
                store.Accounts.Remove(store.LastUsername);
            }

            store.LastUsername = string.Empty;
            WriteStore(store);
        }
    }

    // ── Storage I/O ───────────────────────────────────────────────────────────

    private static CredentialsStore ReadStore()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new CredentialsStore();
            }

            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<CredentialsStore>(json) ?? new CredentialsStore();
        }
        catch
        {
            return new CredentialsStore();
        }
    }

    private static void WriteStore(CredentialsStore store)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(store, _opts));
        }
        catch
        {
            /* non bloquant */
        }
    }

    // ── DPAPI ─────────────────────────────────────────────────────────────────

    private static string? TryEncrypt(string plaintext)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryDecrypt(string encrypted)
    {
        if (string.IsNullOrEmpty(encrypted) || !OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(encrypted);
            var plaintext = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch
        {
            // Corruption, clé DPAPI changée (réinstallation Windows, copie sur autre PC…), etc.
            return null;
        }
    }

    // ── Internal DTOs ─────────────────────────────────────────────────────────

    private sealed class CredentialsStore
    {
        public string LastUsername { get; set; } = string.Empty;

        public Dictionary<string, AccountEntry> Accounts { get; init; } = new();
    }

    private sealed class AccountEntry
    {
        public string EncryptedPassword { get; init; } = string.Empty;
    }
}