using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Minark.Client.Models;
using Minark.Client.Services.Interfaces;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Minark.Client.Services;

/// <summary>
///     Télécharge et installe les fichiers du jeu à partir du manifeste serveur.
///     Supporte la pause, la reprise et la récupération après crash.
///     appsettings.json :
///     "Game": {
///     "InstallPath":  "C:/Games/Minark",
///     "ManifestUrl":  "/game/game-manifest.json",
///     "DownloadBase": "/game/"
///     }
/// </summary>
public sealed class GameUpdaterService : IGameUpdaterService, IDisposable
{
    private const string VersionFile = ".minark-version";
    private const string DownloadStateFile = ".minark-download-state.json";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true, WriteIndented = false };
    private readonly string _defaultInstallPath;
    private readonly string _downloadBase;

    private readonly HttpClient _http;
    private readonly ILogger<GameUpdaterService> _logger;
    private readonly string _manifestUrl;
    private readonly PreferencesService _prefs;

    public GameUpdaterService(IConfiguration config, PreferencesService prefs, ILogger<GameUpdaterService> logger)
    {
        _logger = logger;
        _prefs = prefs;

        var baseUrl = (config["Web:BaseUrl"] ?? "http://localhost:8080").TrimEnd('/');

        _defaultInstallPath = config["Game:InstallPath"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Minark", "Game");

        _manifestUrl = baseUrl + "/" + (config["Game:ManifestUrl"] ?? "game/game-manifest.json").TrimStart('/');
        _downloadBase = baseUrl + "/" + (config["Game:DownloadBase"] ?? "game/").TrimStart('/');
        if (!_downloadBase.EndsWith('/'))
        {
            _downloadBase += '/';
        }

        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        _logger.LogInformation("GameUpdater — init: installPath={P}, manifestUrl={M}, downloadBase={D}",
            InstallPath, _manifestUrl, _downloadBase);
    }

    private string DownloadStatePath => Path.Combine(InstallPath, DownloadStateFile);

    public void Dispose()
    {
        _http.Dispose();
    }

    // ── InstallPath ───────────────────────────────────────────────────────────

    public string InstallPath
    {
        get => _prefs.GetGameInstallPath() ?? _defaultInstallPath;
        set
        {
            _prefs.SetGameInstallPath(value);
            _logger.LogInformation("GameUpdater: install path changed → {P}", value);
        }
    }

    // ── InstalledVersion ──────────────────────────────────────────────────────

    public string? InstalledVersion
    {
        get
        {
            var f = Path.Combine(InstallPath, VersionFile);
            var exists = File.Exists(f);
            _logger.LogDebug("GameUpdater: InstalledVersion — versionFile={F} exists={E}", f, exists);
            return exists ? File.ReadAllText(f).Trim() : null;
        }
        private set
        {
            _logger.LogDebug("GameUpdater: writing InstalledVersion={V} to {P}", value, InstallPath);
            Directory.CreateDirectory(InstallPath);
            File.WriteAllText(Path.Combine(InstallPath, VersionFile), value ?? "");
        }
    }

    // ── CheckForUpdatesAsync ──────────────────────────────────────────────────

    public async Task<UpdateCheckResult?> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("GameUpdater: CheckForUpdates — installPath={P}", InstallPath);
        try
        {
            try
            {
                Directory.CreateDirectory(InstallPath);
                _logger.LogDebug("GameUpdater: install directory created/verified: {P}", InstallPath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "GameUpdater: CheckForUpdates — accès refusé à {P}", InstallPath);
                return null;
            }

            var manifest = await FetchManifestAsync(ct);
            _logger.LogInformation("GameUpdater: manifest fetched — version={V}, files={N}",
                manifest.Version, manifest.Files.Count);

            // ⚠️ DEBUG : vérifier que les entrées du manifeste sont bien désérialisées
            foreach (var f in manifest.Files.Take(5))
            {
                _logger.LogDebug("GameUpdater: manifest entry — path={Path}, archive={Archive}, size={Size}, sha256={Hash}",
                    f.Path, f.Archive, f.Size, f.Sha256);
            }

            var pending = await GetPendingFilesAsync(manifest, ct);
            _logger.LogInformation("GameUpdater: {N} fichier(s) en attente de mise à jour", pending.Count);

            return new UpdateCheckResult(
                pending.Count > 0 || InstalledVersion != manifest.Version,
                manifest.Version,
                InstalledVersion,
                pending.Count,
                pending.Sum(e => e.Size));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GameUpdater: CheckForUpdates failed");
            return null;
        }
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    public async Task<UpdateResult> UpdateAsync(
        IProgress<UpdateProgress> progress,
        CancellationToken ct = default)
    {
        _logger.LogInformation("GameUpdater: UpdateAsync démarré — installPath={P}", InstallPath);
        try
        {
            // Vérifier l'accès en écriture avant tout
            try
            {
                _logger.LogDebug("GameUpdater: test d'écriture dans {P}", InstallPath);
                Directory.CreateDirectory(InstallPath);
                var testFile = Path.Combine(InstallPath, ".write-test");
                await File.WriteAllTextAsync(testFile, "", ct);
                File.Delete(testFile);
                _logger.LogDebug("GameUpdater: test d'écriture OK");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "GameUpdater: accès refusé au dossier {P}", InstallPath);
                return new UpdateResult(false,
                    $"Accès refusé au dossier : {InstallPath}\nChoisissez un autre dossier d'installation.");
            }

            // 1. Manifeste
            progress.Report(new UpdateProgress(UpdatePhase.CheckingManifest, 0, 0, 0, 0));
            _logger.LogInformation("GameUpdater: récupération du manifeste depuis {U}", _manifestUrl);
            var manifest = await FetchManifestAsync(ct);
            _logger.LogInformation("GameUpdater: manifeste OK — version={V}, {N} fichier(s)",
                manifest.Version, manifest.Files.Count);

            // ⚠️ DEBUG : vérifier la désérialisation du manifeste
            if (manifest.Files.Count == 0)
            {
                _logger.LogWarning("GameUpdater: ⚠️ manifeste vide ! Vérifiez le JSON retourné par le serveur.");
            }

            foreach (var f in manifest.Files)
            {
                _logger.LogDebug("GameUpdater: entry — path='{Path}' archive='{Archive}' size={Size} sha256='{Sha256}'",
                    f.Path, f.Archive, f.Size, f.Sha256);

                // ⚠️ Détecter les entrées vides (bug de désérialisation)
                if (string.IsNullOrEmpty(f.Path))
                {
                    _logger.LogError("GameUpdater: ⚠️ entrée avec Path vide ! Problème de désérialisation GameFileEntry.");
                }

                if (string.IsNullOrEmpty(f.Archive))
                {
                    _logger.LogError("GameUpdater: ⚠️ entrée avec Archive null ! Problème de désérialisation GameFileEntry.");
                }
            }

            // 2. Diff
            progress.Report(new UpdateProgress(UpdatePhase.ComparingFiles, 0, manifest.Files.Count, 0, 0));
            var pending = await GetPendingFilesAsync(manifest, ct);
            _logger.LogInformation("GameUpdater: diff terminé — {N} fichier(s) à télécharger", pending.Count);

            if (pending.Count == 0)
            {
                InstalledVersion = manifest.Version;
                ClearDownloadState();
                progress.Report(new UpdateProgress(UpdatePhase.Done, 0, 0, 0, 0));
                _logger.LogInformation("GameUpdater: déjà à jour ({V})", manifest.Version);
                return new UpdateResult(true);
            }

            // 3. Charger l'état de reprise
            var state = LoadDownloadState(manifest.Version);

            var todo = pending.Where(e => !state.CompletedFiles.Contains(e.Path)).ToList();
            var totalBytes = pending.Sum(e => e.Size);
            var doneBytes = pending.Where(e => state.CompletedFiles.Contains(e.Path)).Sum(e => e.Size);
            var doneFiles = state.CompletedFiles.Count;

            _logger.LogInformation("GameUpdater: {Todo} fichier(s) à télécharger ({Done} déjà complétés), {TotalMb:F1} Mo total",
                todo.Count, doneFiles, totalBytes / 1024.0 / 1024.0);

            // 4. Télécharger chaque archive et extraire
            foreach (var entry in todo)
            {
                ct.ThrowIfCancellationRequested();

                _logger.LogInformation("GameUpdater: ▶ téléchargement de '{Path}' (archive='{Archive}', {Size} octets)",
                    entry.Path, entry.Archive, entry.Size);

                // ⚠️ Détecter un archive null/vide avant d'essayer de télécharger
                if (string.IsNullOrWhiteSpace(entry.Archive))
                {
                    _logger.LogError("GameUpdater: ⛔ Archive vide pour l'entrée '{Path}' — arrêt", entry.Path);
                    return new UpdateResult(false, $"Entrée de manifeste invalide (archive vide) : {entry.Path}");
                }

                var archiveUrl = _downloadBase + entry.Archive;
                _logger.LogDebug("GameUpdater: URL d'archive = {Url}", archiveUrl);

                progress.Report(new UpdateProgress(
                    UpdatePhase.Downloading, doneFiles, pending.Count, doneBytes, totalBytes, entry.Path));

                byte[] archiveBytes;
                try
                {
                    archiveBytes = await DownloadAsync(archiveUrl,
                        received => progress.Report(new UpdateProgress(
                            UpdatePhase.Downloading, doneFiles, pending.Count,
                            doneBytes + received, totalBytes, entry.Path)),
                        ct);
                    _logger.LogInformation("GameUpdater: téléchargement OK — {Bytes} octets reçus pour '{Archive}'",
                        archiveBytes.Length, entry.Archive);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "GameUpdater: ⛔ échec HTTP pour {Url}", archiveUrl);
                    return new UpdateResult(false, $"Erreur téléchargement : {ex.Message}");
                }

                progress.Report(new UpdateProgress(
                    UpdatePhase.Extracting, doneFiles, pending.Count, doneBytes, totalBytes, entry.Path));

                _logger.LogDebug("GameUpdater: extraction de '{Archive}' vers '{Dest}'", entry.Archive, InstallPath);
                try
                {
                    ExtractTarGz(archiveBytes, InstallPath);
                    _logger.LogInformation("GameUpdater: extraction OK — '{Archive}'", entry.Archive);
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "GameUpdater: ⛔ accès refusé à l'extraction dans '{Dest}'", InstallPath);
                    return new UpdateResult(false,
                        $"Accès refusé au dossier : {InstallPath}\nChoisissez un autre dossier d'installation.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GameUpdater: ⛔ erreur extraction '{Archive}'", entry.Archive);
                    return new UpdateResult(false, $"Erreur extraction : {ex.Message}");
                }

                var localPath = ToLocalPath(entry.Path);
                _logger.LogDebug("GameUpdater: vérification hash — {LocalPath}", localPath);

                if (!File.Exists(localPath))
                {
                    _logger.LogError("GameUpdater: ⛔ fichier introuvable après extraction : '{LocalPath}'. " +
                                     "Vérifiez que le chemin dans le manifeste ('{EntryPath}') correspond au chemin dans l'archive tar.gz.",
                        localPath, entry.Path);
                    return new UpdateResult(false, $"Fichier introuvable après extraction : {entry.Path}");
                }

                var hash = await ComputeSha256Async(localPath, ct);
                _logger.LogDebug("GameUpdater: hash calculé={Got}, attendu={Expected}", hash, entry.Sha256);
                if (hash != entry.Sha256)
                {
                    _logger.LogWarning("GameUpdater: ⛔ hash mismatch: '{P}' expected={E} got={G}",
                        entry.Path, entry.Sha256, hash);
                    return new UpdateResult(false, $"Vérification échouée : {entry.Path}");
                }

                _logger.LogInformation("GameUpdater: ✅ hash OK — '{Path}'", entry.Path);

                state.CompletedFiles.Add(entry.Path);
                SaveDownloadState(state);

                doneBytes += entry.Size;
                doneFiles++;
            }

            // 5. Nettoyage des fichiers orphelins (supprimés du manifeste)
            CleanupOrphanedFiles(manifest);

            // 6. Tout est OK
            InstalledVersion = manifest.Version;
            ClearDownloadState();

            progress.Report(new UpdateProgress(UpdatePhase.Done, doneFiles, pending.Count, doneBytes, totalBytes));
            _logger.LogInformation("GameUpdater: ✅ mise à jour terminée → version {V}", manifest.Version);
            return new UpdateResult(true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("GameUpdater: téléchargement mis en pause (OperationCanceledException)");
            return new UpdateResult(false, "Téléchargement mis en pause.");
        }
        catch (Exception ex) when (ex.InnerException is OperationCanceledException or SocketException)
        {
            _logger.LogInformation(ex, "GameUpdater: connexion interrompue (pause) — inner={Inner}", ex.InnerException?.GetType().Name);
            return new UpdateResult(false, "Téléchargement mis en pause.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GameUpdater: ⛔ UpdateAsync failed — {Type}: {Message}", ex.GetType().Name, ex.Message);
            return new UpdateResult(false, $"Erreur : {ex.Message}");
        }
    }

    private DownloadState LoadDownloadState(string manifestVersion)
    {
        _logger.LogDebug("GameUpdater: LoadDownloadState — version={V}, path={P}", manifestVersion, DownloadStatePath);
        try
        {
            if (!File.Exists(DownloadStatePath))
            {
                _logger.LogDebug("GameUpdater: pas d'état de reprise trouvé, départ à zéro");
                return new DownloadState { ManifestVersion = manifestVersion };
            }

            var state = JsonSerializer.Deserialize<DownloadState>(File.ReadAllText(DownloadStatePath), JsonOpts);
            if (state?.ManifestVersion != manifestVersion)
            {
                _logger.LogInformation("GameUpdater: version de l'état ({Old}) != version manifeste ({New}), reset",
                    state?.ManifestVersion, manifestVersion);
                return new DownloadState { ManifestVersion = manifestVersion };
            }

            _logger.LogInformation("GameUpdater: reprise — {N} fichier(s) déjà complétés", state.CompletedFiles.Count);
            return state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GameUpdater: impossible de lire l'état de reprise, reset");
            return new DownloadState { ManifestVersion = manifestVersion };
        }
    }

    private void SaveDownloadState(DownloadState state)
    {
        try
        {
            Directory.CreateDirectory(InstallPath);
            File.WriteAllText(DownloadStatePath, JsonSerializer.Serialize(state, JsonOpts));
            _logger.LogDebug("GameUpdater: état de reprise sauvegardé ({N} fichiers)", state.CompletedFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GameUpdater: impossible de sauvegarder l'état");
        }
    }

    private void ClearDownloadState()
    {
        try
        {
            if (File.Exists(DownloadStatePath))
            {
                File.Delete(DownloadStatePath);
                _logger.LogDebug("GameUpdater: état de reprise supprimé");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GameUpdater: impossible de supprimer l'état de reprise");
        }
    }

    // ── Privé ─────────────────────────────────────────────────────────────────

    private async Task<GameManifest> FetchManifestAsync(CancellationToken ct)
    {
        _logger.LogDebug("GameUpdater: FetchManifest — GET {Url}", _manifestUrl);
        var json = await _http.GetStringAsync(_manifestUrl, ct);
        _logger.LogDebug("GameUpdater: manifeste JSON reçu ({Len} chars) : {Json}",
            json.Length, json.Length <= 500 ? json : json[..500] + "…");
        return JsonSerializer.Deserialize<GameManifest>(json, JsonOpts)
               ?? throw new InvalidDataException("Manifeste JSON invalide.");
    }

    private async Task<List<GameFileEntry>> GetPendingFilesAsync(GameManifest manifest, CancellationToken ct)
    {
        _logger.LogDebug("GameUpdater: GetPendingFiles — {N} entrées dans le manifeste", manifest.Files.Count);
        var result = new List<GameFileEntry>();
        await Parallel.ForEachAsync(manifest.Files,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (entry, token) =>
            {
                var local = ToLocalPath(entry.Path);
                if (!File.Exists(local))
                {
                    _logger.LogDebug("GameUpdater: à télécharger (absent) — '{Path}' → '{Local}'", entry.Path, local);
                    lock (result)
                    {
                        result.Add(entry);
                    }

                    return;
                }

                var hash = await ComputeSha256Async(local, token);
                if (hash != entry.Sha256)
                {
                    _logger.LogDebug("GameUpdater: à télécharger (hash différent) — '{Path}' local={LocalHash} attendu={Expected}",
                        entry.Path, hash, entry.Sha256);
                    lock (result)
                    {
                        result.Add(entry);
                    }
                }
                else
                {
                    _logger.LogDebug("GameUpdater: à jour — '{Path}'", entry.Path);
                }
            });
        return result;
    }

    private async Task<byte[]> DownloadAsync(string url, Action<long> onProgress, CancellationToken ct)
    {
        _logger.LogDebug("GameUpdater: DownloadAsync — GET {Url}", url);
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        _logger.LogDebug("GameUpdater: réponse HTTP {Status} — Content-Length={Len}",
            (int)resp.StatusCode, resp.Content.Headers.ContentLength);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var buf = new byte[81920];
        long received = 0;
        int read;
        while ((read = await stream.ReadAsync(buf, ct)) > 0)
        {
            await ms.WriteAsync(buf.AsMemory(0, read), ct);
            received += read;
            onProgress(received);
        }

        _logger.LogDebug("GameUpdater: DownloadAsync terminé — {Bytes} octets", received);
        return ms.ToArray();
    }

    private static void ExtractTarGz(byte[] data, string destDir)
    {
        Directory.CreateDirectory(destDir);

        using var ms = new MemoryStream(data);
        using var reader = ReaderFactory.OpenReader(ms);
        while (reader.MoveToNextEntry())
        {
            if (reader.Entry.IsDirectory)
            {
                var dirPath = Path.Combine(destDir,
                    reader.Entry.Key.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(dirPath);
                continue;
            }

            var filePath = Path.Combine(destDir,
                reader.Entry.Key.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            // ✅ WriteEntryToFile au lieu de WriteEntryToDirectory
            reader.WriteEntryToFile(filePath, new ExtractionOptions
            {
                Overwrite = true
            });
        }
    }

    // ── Nettoyage des fichiers orphelins ─────────────────────────────────────

    /// <summary>
    ///     Supprime les fichiers présents dans InstallPath mais absents du manifeste.
    ///     Supprime aussi les dossiers vides laissés après la suppression des fichiers.
    /// </summary>
    private void CleanupOrphanedFiles(GameManifest manifest)
    {
        try
        {
            if (!Directory.Exists(InstallPath))
            {
                return;
            }

            // Construire le set des chemins locaux attendus (+ les fichiers internes du service)
            var expectedPaths = manifest.Files
                .Select(e => Path.GetFullPath(ToLocalPath(e.Path)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Fichiers internes à ne jamais supprimer
            expectedPaths.Add(Path.GetFullPath(Path.Combine(InstallPath, VersionFile)));
            expectedPaths.Add(Path.GetFullPath(DownloadStatePath));

            var allFiles = Directory.GetFiles(InstallPath, "*", SearchOption.AllDirectories);
            var deleted = 0;

            foreach (var file in allFiles)
            {
                var fullPath = Path.GetFullPath(file);
                if (expectedPaths.Contains(fullPath))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                    deleted++;
                    _logger.LogInformation("GameUpdater: 🗑 orphelin supprimé — {File}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GameUpdater: impossible de supprimer l'orphelin {File}", file);
                }
            }

            // Supprimer les dossiers vides (du plus profond vers la racine)
            var allDirs = Directory.GetDirectories(InstallPath, "*", SearchOption.AllDirectories)
                .OrderByDescending(d => d.Length); // plus profonds en premier

            var deletedDirs = 0;
            foreach (var dir in allDirs)
            {
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                        deletedDirs++;
                        _logger.LogDebug("GameUpdater: 🗑 dossier vide supprimé — {Dir}", dir);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GameUpdater: impossible de supprimer le dossier vide {Dir}", dir);
                }
            }

            _logger.LogInformation("GameUpdater: nettoyage terminé — {F} fichier(s) et {D} dossier(s) orphelins supprimés",
                deleted, deletedDirs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GameUpdater: nettoyage des orphelins échoué (non critique)");
        }
    }

    private string ToLocalPath(string relative)
    {
        return Path.Combine(InstallPath, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct = default)
    {
        await using var s = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(s, ct));
    }

    // ── État de téléchargement (reprise après crash) ───────────────────────────

    private sealed class DownloadState
    {
        public string ManifestVersion { get; init; } = string.Empty;
        public HashSet<string> CompletedFiles { get; init; } = [];
    }
}