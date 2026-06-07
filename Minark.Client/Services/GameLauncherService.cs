using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using Minark.Client.Services.Interfaces;

namespace Minark.Client.Services;

/// <summary>
///     Lance le jeu compilé (Unity) et lui passe le token Minark via argument CLI.
///     Unity lit --minark-token au démarrage, appelle l'API Web pour valider
///     et récupère l'adresse du GameServer.
/// </summary>
public class GameLauncherService(
    IAuthClientService auth,
    IGameUpdaterService updater,
    IConfiguration config,
    ILogger<GameLauncherService> logger) : IGameLauncherService
{
    private Process? _gameProcess;

    /// <summary>
    ///     Chemin de l'exe : InstallPath\Minark.exe
    ///     Fallback sur Game:ExecutablePath dans appsettings.json si défini.
    /// </summary>
    private string ExecutablePath
    {
        get
        {
            var fromConfig = config["Game:ExecutablePath"];
            if (!string.IsNullOrWhiteSpace(fromConfig))
            {
                return fromConfig;
            }

            return Path.Combine(updater.InstallPath, "Minark.exe");
        }
    }

    public bool IsRunning => _gameProcess is { HasExited: false };

    public event Action? GameExited;

    public Task<GameLaunchResult> LaunchAsync()
    {
        if (!auth.IsLoggedIn || string.IsNullOrWhiteSpace(auth.Token))
        {
            return Task.FromResult(new GameLaunchResult(false, "Vous devez être connecté pour lancer le jeu."));
        }

        if (IsRunning)
        {
            BringToFront();
            return Task.FromResult(new GameLaunchResult(true));
        }

        var execPath = ExecutablePath;

        if (!File.Exists(execPath))
        {
            return Task.FromResult(new GameLaunchResult(false,
                $"Exécutable introuvable : {execPath}"));
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = execPath,
                // Le token est le seul secret à transmettre.
                // L'URL de l'API Web est configurée dans le build Unity.
                Arguments = $"--minark-token {auth.Token}",
                UseShellExecute = false
            };

            _gameProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _gameProcess.Exited += OnGameExited;
            _gameProcess.Start();

            logger.LogInformation("Game launched — pid={Pid}, user={User}",
                _gameProcess.Id, auth.CurrentUser?.Username);

            return Task.FromResult(new GameLaunchResult(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch game");
            return Task.FromResult(new GameLaunchResult(false, $"Impossible de lancer le jeu : {ex.Message}"));
        }
    }

    private void OnGameExited(object? sender, EventArgs e)
    {
        logger.LogInformation("Game exited (code={Code})", _gameProcess?.ExitCode);
        _gameProcess = null;
        GameExited?.Invoke();
    }

    private void BringToFront()
    {
        try
        {
            _gameProcess?.WaitForInputIdle(500);
        }
        catch
        {
            /* ignore */
        }
    }
}