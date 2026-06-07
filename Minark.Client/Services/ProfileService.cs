using System.IO;
using System.Text;
using System.Text.Json;
using Serilog;

namespace Minark.Client.Services;

/// <summary>
///     Gère l'isolation des données par compte utilisateur.
///     <para>Structure disque :</para>
///     <code>
///     %LOCALAPPDATA%\Minark\
///         credentials.json                   (global : comptes sauvés, DPAPI)
///         last-used.json                     (global : dernier user loggé)
///         profiles\
///             sheppeR\                       (profil d'un user)
///                 userstatus.json
///                 logs\
///             bootstrap-12345\               (profil temporaire, PID = 12345)
///                 logs\
///     </code>
/// </summary>
public sealed class ProfileService
{
    private static readonly string _profilesDir;
    private static readonly string _lastUsedPath;

    static ProfileService()
    {
        // L'ordre ici est garanti par le runtime — aucune dépendance sur l'ordre textuel.
        RootDirectory = ComputeRootDirectory();
        _profilesDir = Path.Combine(RootDirectory, "profiles");
        _lastUsedPath = Path.Combine(RootDirectory, "last-used.json");
    }

    // ── Constructeur d'instance ───────────────────────────────────────────────

    public ProfileService()
    {
        Directory.CreateDirectory(_profilesDir);
        PurgeOrphanBootstraps();

        IsBootstrap = true;
        Name = $"bootstrap-{Environment.ProcessId}";
        DataDirectory = Path.Combine(_profilesDir, Name);
        Directory.CreateDirectory(DataDirectory);
    }
    // ── Membres statiques ─────────────────────────────────────────────────────
    //
    // Initialisés dans le constructeur statique explicite ci-dessous.
    // Un cctor statique garantit l'ordre d'exécution indépendamment de l'ordre
    // de déclaration des membres — ReSharper, Roslyn ou tout autre outil de
    // reformatage ne peut donc pas casser l'initialisation en réordonnant les champs.

    public static string RootDirectory { get; }

    // ── Propriétés d'instance ─────────────────────────────────────────────────

    /// <summary>Nom du profil courant (username, ou "bootstrap-&lt;pid&gt;" avant login).</summary>
    public string Name { get; private set; }

    /// <summary>Dossier racine des données de ce profil.</summary>
    public string DataDirectory { get; private set; }

    /// <summary>Vrai tant qu'aucun login n'a basculé vers un profil définitif.</summary>
    public bool IsBootstrap { get; private set; }

    /// <summary>Déclenché après un basculement bootstrap → profil user (post-login).</summary>
    public event Action? ProfileSwitched;

    // ── API publique ──────────────────────────────────────────────────────────

    /// <summary>Chemin d'un fichier de données dans ce profil.</summary>
    public string GetFilePath(string fileName)
    {
        return Path.Combine(DataDirectory, fileName);
    }

    /// <summary>Chemin d'un sous-dossier dans ce profil (créé si absent).</summary>
    public string GetSubDirectory(string subDirName)
    {
        var full = Path.Combine(DataDirectory, subDirName);
        Directory.CreateDirectory(full);
        return full;
    }

    /// <summary>
    ///     Appelé après un login réussi. Bascule le profil vers celui du <paramref name="username" /> :
    ///     migre le contenu du dossier bootstrap vers le dossier définitif, supprime le bootstrap,
    ///     et enregistre le username comme "dernier utilisé".
    /// </summary>
    public void SwitchToUserProfile(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        PersistLastUsed(username);

        if (!IsBootstrap)
        {
            return; // déjà sur un profil user
        }

        var safeName = SanitizeProfileName(username);
        var targetDir = Path.Combine(_profilesDir, safeName);
        var sourceDir = DataDirectory;

        try
        {
            Directory.CreateDirectory(targetDir);
            MigrateContents(sourceDir, targetDir);

            try
            {
                // Fermer Serilog pour libérer les fichiers log avant suppression
                Log.CloseAndFlush();
                Directory.Delete(sourceDir, true);
            }
            catch
            {
                // Non bloquant : fichier log ouvert → sera purgé au prochain démarrage.
            }

            Name = safeName;
            DataDirectory = targetDir;
            IsBootstrap = false;
            ProfileSwitched?.Invoke();
        }
        catch
        {
            // Switch échoué → on reste en bootstrap, l'app continue.
        }
    }

    // ── Méthodes statiques privées ────────────────────────────────────────────

    /// <summary>
    ///     Calcule le dossier racine des données Minark.
    ///     Utilise %LOCALAPPDATA%, avec deux fallbacks pour les environnements exotiques.
    /// </summary>
    private static string ComputeRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                localAppData = Path.Combine(userProfile, "AppData", "Local");
            }
        }

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Environment.CurrentDirectory;
        }

        return Path.Combine(localAppData, "Minark");
    }

    private static void MigrateContents(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(targetDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

            try
            {
                File.Copy(file, destination, true);
            }
            catch
            {
                /* fichier verrouillé → on passe */
            }
        }
    }

    private static void PurgeOrphanBootstraps()
    {
        if (!Directory.Exists(_profilesDir))
        {
            return;
        }

        foreach (var dir in Directory.EnumerateDirectories(_profilesDir, "bootstrap-*"))
        {
            try
            {
                Directory.Delete(dir, true);
            }
            catch
            {
                /* instance active → retenté au prochain démarrage */
            }
        }
    }

    private static void PersistLastUsed(string username)
    {
        try
        {
            Directory.CreateDirectory(RootDirectory);
            File.WriteAllText(_lastUsedPath,
                JsonSerializer.Serialize(new LastUsedDocument { Username = username }));
        }
        catch
        {
            /* non bloquant */
        }
    }

    private static string SanitizeProfileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var buffer = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            buffer.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        var cleaned = buffer.ToString().Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
    }

    // ── Types internes ────────────────────────────────────────────────────────

    private sealed class LastUsedDocument
    {
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string Username { get; set; } = string.Empty;
    }
}