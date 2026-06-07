namespace Minark.Client.Services.Interfaces;

public interface IGameUpdaterService
{
    /// <summary>Dossier d'installation actif.</summary>
    string InstallPath { get; set; }

    /// <summary>Version actuellement installee, ou null si pas encore installe.</summary>
    string? InstalledVersion { get; }

    /// <summary>Verifie si une mise a jour est disponible sans rien telecharger.</summary>
    Task<UpdateCheckResult?> CheckForUpdatesAsync(CancellationToken ct = default);

    /// <summary>Telecharge et installe les fichiers manquants ou modifies.</summary>
    Task<UpdateResult> UpdateAsync(IProgress<UpdateProgress> progress, CancellationToken ct = default);
}

public record UpdateCheckResult(
    bool UpdateAvailable,
    string RemoteVersion,
    string? LocalVersion,
    int FilesToDownload,
    long BytesToDownload);

public record UpdateResult(bool Success, string? ErrorMessage = null);

public record UpdateProgress(
    UpdatePhase Phase,
    int FileDone,
    int FileTotal,
    long BytesDone,
    long BytesTotal,
    string CurrentFile = "")
{
    public double Percent => BytesTotal == 0 ? 0 : (double)BytesDone / BytesTotal * 100;
}

public enum UpdatePhase
{
    CheckingManifest,
    ComparingFiles,
    Downloading,
    Extracting,
    Verifying,
    Done
}