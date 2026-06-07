using System.IO;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Windows.Threading;
using Microsoft.Win32;
using Minark.Client.Services.Interfaces;
using ReactiveUI;

namespace Minark.Client.ViewModels.Pages;

public class LibraryViewModel : ViewModelBase
{
    private readonly IGameLauncherService _launcher;
    private readonly IScheduler _mainScheduler;
    private readonly IGameUpdaterService _updater;
    private long _bytesDone;
    private long _bytesTotal;
    private CancellationTokenSource? _cts;
    private string _installPath;

    public LibraryViewModel(IGameUpdaterService updater, IGameLauncherService launcher, ShellViewModel shell)
    {
        _updater = updater;
        _launcher = launcher;
        _mainScheduler = new DispatcherScheduler(Dispatcher.CurrentDispatcher);
        _installPath = updater.InstallPath;

        PlayCommand = ReactiveCommand.CreateFromTask(PlayAsync,
            this.WhenAnyValue(x => x.CanPlay), _mainScheduler);
        DownloadCommand = ReactiveCommand.CreateFromTask(DownloadAsync,
            this.WhenAnyValue(x => x.CanDownload), _mainScheduler);
        ResumeCommand = ReactiveCommand.CreateFromTask(DownloadAsync,
            this.WhenAnyValue(x => x.CanResume), _mainScheduler);
        CancelCommand = ReactiveCommand.Create(PauseDownload,
            this.WhenAnyValue(x => x.CanCancel), _mainScheduler);
        ChooseInstallFolderCommand = ReactiveCommand.Create(ChooseInstallFolder,
            this.WhenAnyValue(x => x.IsWorking).Select(w => !w), _mainScheduler);
        GoToDownloadCommand = ReactiveCommand.Create(
            () => { shell.NavigateCommand.Execute("Downloads").Subscribe(); },
            outputScheduler: _mainScheduler);
        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync,
            this.WhenAnyValue(x => x.IsWorking).Select(w => !w), _mainScheduler);

        _ = Task.Run(CheckInstallStatusAsync);
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public string StatusText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Vérification des mises à jour…";

    public string SubStatusText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public double ProgressValue
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsIndeterminate
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool CanPlay
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CanDownload
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CanCancel
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool CanResume
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsWorking
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string CurrentFile
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string InstalledVersion
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "—";

    public string RemoteVersion
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "—";

    public string InstallPath
    {
        get => _installPath;
        private set => this.RaiseAndSetIfChanged(ref _installPath, value);
    }

    public string BytesSummary => $"{FormatBytes(_bytesDone)} / {FormatBytes(_bytesTotal)}";

    public double FileProgressValue
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string FileProgressText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ReactiveCommand<Unit, Unit> PlayCommand { get; }
    public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> ResumeCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }
    public ReactiveCommand<Unit, Unit> ChooseInstallFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> GoToDownloadCommand { get; }
    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }

    // ── Logic ─────────────────────────────────────────────────────────────────

    private void UI(Action action)
    {
        _mainScheduler.Schedule(action);
    }

    private async Task CheckInstallStatusAsync()
    {
        var installed = File.Exists(Path.Combine(_updater.InstallPath, "Minark.exe"));

        if (!installed)
        {
            UI(() =>
            {
                InstalledVersion = "Non installé";
                SetIdle("Jeu non installé.", downloadable: true);
            });
            return;
        }

        // Jeu présent → vérifier les MAJ directement
        UI(() =>
        {
            InstalledVersion = _updater.InstalledVersion ?? "Installé";
            SetWorking("Vérification des mises à jour…");
        });

        await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        UI(() => SetWorking("Vérification des mises à jour…"));
        var check = await _updater.CheckForUpdatesAsync();

        UI(() =>
        {
            if (check is null)
            {
                var installed = File.Exists(Path.Combine(_updater.InstallPath, "Minark.exe"));
                InstalledVersion = installed ? _updater.InstalledVersion ?? "Installé" : "Non installé";
                SetIdle("Serveur inaccessible.", installed);
                return;
            }

            InstalledVersion = check.LocalVersion ?? "Non installé";
            RemoteVersion = check.RemoteVersion;

            if (!check.UpdateAvailable)
            {
                SetIdle($"Le jeu est à jour ({check.RemoteVersion})", true);
                return;
            }

            var hasPartial = File.Exists(Path.Combine(_updater.InstallPath, ".minark-download-state.json"));
            var isFirst = check.LocalVersion is null;

            var label = isFirst
                ? $"Installation requise ({FormatBytes(check.BytesToDownload)})"
                : $"Mise à jour disponible : {check.RemoteVersion} — {FormatBytes(check.BytesToDownload)}";

            SubStatusText = isFirst
                ? $"{check.FilesToDownload} fichier(s) à télécharger"
                : $"{check.FilesToDownload} fichier(s) modifié(s)";

            if (hasPartial)
            {
                SetIdle(label + " (téléchargement interrompu)", resumable: true);
            }
            else
            {
                SetIdle(label, downloadable: true);
            }
        });
    }

    private async Task DownloadAsync()
    {
        _cts = new CancellationTokenSource();
        UI(() =>
        {
            SetWorking("Téléchargement en cours…");
            CanCancel = true;
        });

        var lastUiUpdate = DateTime.MinValue;
        UpdateProgress? lastProgress = null;

        var progress = new Progress<UpdateProgress>(p =>
        {
            lastProgress = p;
            var now = DateTime.UtcNow;
            if ((now - lastUiUpdate).TotalMilliseconds < 100 && p.Phase == UpdatePhase.Downloading)
            {
                return;
            }

            lastUiUpdate = now;
            UI(() => OnProgress(p));
        });

        var result = await _updater.UpdateAsync(progress, _cts.Token);
        if (lastProgress is not null)
        {
            UI(() => OnProgress(lastProgress));
        }

        UI(() =>
        {
            CanCancel = false;
            if (result.Success)
            {
                InstalledVersion = _updater.InstalledVersion ?? "—";
                ProgressValue = FileProgressValue = 100;
                IsIndeterminate = false;
                SetIdle("Jeu à jour — prêt à jouer !", true);
            }
            else if (result.ErrorMessage == "Téléchargement mis en pause.")
            {
                SetIdle("Téléchargement en pause — cliquez sur Reprendre.", resumable: true);
            }
            else
            {
                SetIdle($"Échec : {result.ErrorMessage}",
                    _updater.InstalledVersion is not null,
                    resumable: _updater.InstalledVersion is null);
            }
        });
    }

    private void OnProgress(UpdateProgress p)
    {
        _bytesDone = p.BytesDone;
        _bytesTotal = p.BytesTotal;
        this.RaisePropertyChanged(nameof(BytesSummary));

        switch (p.Phase)
        {
            case UpdatePhase.CheckingManifest:
                StatusText = "Récupération du manifeste…";
                IsIndeterminate = ProgressValue == 0;
                break;
            case UpdatePhase.ComparingFiles:
                StatusText = "Comparaison des fichiers…";
                IsIndeterminate = ProgressValue == 0;
                break;
            case UpdatePhase.Downloading:
                StatusText = $"Téléchargement… ({p.FileDone}/{p.FileTotal})";
                CurrentFile = p.CurrentFile;
                IsIndeterminate = false;
                ProgressValue = p.Percent;
                SubStatusText = BytesSummary;
                FileProgressValue = p.FileTotal == 0 ? 0 : (double)p.FileDone / p.FileTotal * 100;
                FileProgressText = $"Fichier {p.FileDone} / {p.FileTotal}";
                break;
            case UpdatePhase.Extracting:
                StatusText = $"Extraction… {p.CurrentFile}";
                IsIndeterminate = false;
                break;
            case UpdatePhase.Verifying:
                StatusText = "Vérification des fichiers…";
                IsIndeterminate = false;
                break;
            case UpdatePhase.Done:
                StatusText = "Terminé !";
                IsIndeterminate = false;
                ProgressValue = FileProgressValue = 100;
                break;
        }
    }

    private async Task PlayAsync()
    {
        var result = await _launcher.LaunchAsync();
        if (!result.Success)
        {
            UI(() => StatusText = result.ErrorMessage ?? "Impossible de lancer le jeu.");
        }
    }

    private void PauseDownload()
    {
        _cts?.Cancel();
        UI(() => StatusText = "Mise en pause…");
    }

    private void ChooseInstallFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choisir le dossier d'installation du jeu",
            InitialDirectory = _updater.InstallPath,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _updater.InstallPath = dialog.FolderName;
        InstallPath = dialog.FolderName;
        _ = Task.Run(CheckInstallStatusAsync);
    }

    // ── State helpers (UI thread only) ────────────────────────────────────────

    private void SetWorking(string status)
    {
        IsWorking = true;
        StatusText = status;
        IsIndeterminate = true;
        ProgressValue = FileProgressValue = 0;
        FileProgressText = string.Empty;
        CurrentFile = string.Empty;
        CanPlay = CanDownload = CanResume = CanCancel = false;
    }

    private void SetIdle(string status, bool ready = false, bool downloadable = false, bool resumable = false)
    {
        IsWorking = false;
        StatusText = status;
        IsIndeterminate = false;
        if (ready)
        {
            ProgressValue = 100;
        }

        CanPlay = ready;
        CanDownload = downloadable;
        CanResume = resumable;
        CanCancel = false;
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} o",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} Ko",
            < 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} Mo",
            _ => $"{bytes / 1024.0 / 1024 / 1024:F2} Go"
        };
    }
}