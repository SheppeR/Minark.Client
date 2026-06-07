using System.ComponentModel;
using System.IO;
using System.Reactive;
using System.Windows.Media;
using iNKORE.UI.WPF.Helpers;
using iNKORE.UI.WPF.Modern;
using Microsoft.Win32;
using Minark.Client.Services;
using Minark.Client.Services.Interfaces;
using Minark.Shared;
using ReactiveUI;
using Serilog;

namespace Minark.Client.ViewModels.Pages;

public class SettingsViewModel : ReactiveObject
{
    private readonly IAuthClientService _auth;
    private readonly PreferencesService _preferences;
    private readonly IProfileClientService _profile;
    private readonly IGameUpdaterService _updater;

    private bool _isLight;
    private bool _updatingAccentColor;

    public SettingsViewModel(
        IAuthClientService auth,
        IProfileClientService profile,
        SoundService sound,
        NotificationService notifications,
        PreferencesService preferences,
        IDialogService dialog,
        IGameUpdaterService updater)
    {
        _auth = auth;
        _profile = profile;
        _preferences = preferences;
        _updater = updater;

        _isLight = ThemeManager.Current.ApplicationTheme != ApplicationTheme.Dark;
        AvatarUrl = auth.CurrentUser?.AvatarUrl ?? string.Empty;
        SoundEnabled = preferences.GetSoundEnabled();
        ToastEnabled = preferences.GetToastEnabled();

        this.WhenAnyValue(x => x.SoundEnabled).Subscribe(v =>
        {
            sound.Enabled = v;
            preferences.SetSoundEnabled(v);
        });
        this.WhenAnyValue(x => x.ToastEnabled).Subscribe(v =>
        {
            notifications.ToastEnabled = v;
            preferences.SetToastEnabled(v);
        });

        this.WhenAnyValue(x => x.PwdLoading).Subscribe(_ => this.RaisePropertyChanged(nameof(PwdNotLoading)));
        this.WhenAnyValue(x => x.AvatarLoading).Subscribe(_ => this.RaisePropertyChanged(nameof(AvatarNotLoading)));

        DispatcherHelper.RunOnMainThread(() =>
        {
            DependencyPropertyDescriptor
                .FromProperty(ThemeManager.ApplicationThemeProperty, typeof(ThemeManager))
                .AddValueChanged(ThemeManager.Current, (_, _) => SyncTheme());
            DependencyPropertyDescriptor
                .FromProperty(ThemeManager.AccentColorProperty, typeof(ThemeManager))
                .AddValueChanged(ThemeManager.Current, (_, _) => SyncAccent());
            SyncTheme();
            SyncAccent();
        });

        var notLoading = this.WhenAnyValue(x => x.PwdLoading, loading => !loading);
        var avatarNotLoading = this.WhenAnyValue(x => x.AvatarLoading, loading => !loading);

        ChangePasswordCommand = ReactiveCommand.CreateFromTask(ChangePasswordAsync, notLoading);
        UpdateAvatarCommand = ReactiveCommand.CreateFromTask<string>(UpdateAvatarAsync, avatarNotLoading);
        ApplyAccentColorCommand = ReactiveCommand.Create<SolidColorBrush>(brush =>
            DispatcherHelper.RunOnMainThread(() => ThemeManager.Current.AccentColor = brush.Color));

        BrowseAvatarCommand = ReactiveCommand.Create(() =>
        {
            var path = dialog.OpenFile(
                "Choisir une photo de profil",
                "Images|*.jpg;*.jpeg;*.png;*.gif;*.webp|Tous les fichiers|*.*");
            if (path is null)
            {
                return;
            }

            SelectedAvatarPath = path;
            AvatarFileName = Path.GetFileName(path);
        });

        UploadSelectedAvatarCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (string.IsNullOrWhiteSpace(SelectedAvatarPath))
            {
                AvatarMessage = "Veuillez d'abord sélectionner un fichier.";
                AvatarError = true;
                return;
            }

            await UpdateAvatarAsync(SelectedAvatarPath);
            if (AvatarSuccess)
            {
                SelectedAvatarPath = null;
            }
        }, avatarNotLoading);

        ChooseInstallFolderCommand = ReactiveCommand.Create(() =>
        {
            var d = new OpenFolderDialog
            {
                Title = "Choisir le dossier d'installation du jeu",
                InitialDirectory = updater.InstallPath,
                Multiselect = false
            };
            if (d.ShowDialog() == true)
            {
                updater.InstallPath = d.FolderName;
                this.RaisePropertyChanged(nameof(GameInstallPath));
            }
        });
    }

    public static IReadOnlyList<SolidColorBrush> AccentColors { get; } =
        new[]
            {
                "#a4c400", "#FFB900", "#FF8C00", "#F7630C", "#DA3B01",
                "#EF6950", "#D13438", "#E74856", "#E81123", "#EA005E",
                "#C239B3", "#9A0089", "#0078D7", "#0063B1", "#8E8CD8",
                "#6B69D6", "#744DA9", "#0099BC", "#00B7C3", "#038387",
                "#00B294", "#00CC6A", "#10893E", "#767676", "#4C4A48"
            }
            .Select(hex => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)))
            .ToArray();

    public bool IsLight
    {
        get => _isLight;
        set
        {
            this.RaiseAndSetIfChanged(ref _isLight, value);
            var theme = value ? ThemeService.AppTheme.Light : ThemeService.AppTheme.Dark;
            DispatcherHelper.RunOnMainThread(() => ThemeService.SetTheme(theme));
            _preferences.SetTheme(theme);
        }
    }

    public Color? AccentColor
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            if (!_updatingAccentColor)
            {
                DispatcherHelper.RunOnMainThread(() => ThemeManager.Current.AccentColor = value);
            }
        }
    }

    public string AvatarUrl
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool AvatarLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool AvatarSuccess
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool AvatarError
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string AvatarMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string OldPassword
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string NewPassword
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string ConfirmPassword
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool PwdLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool PwdSuccess
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool PwdError
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string PwdMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool SoundEnabled
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool ToastEnabled
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool PwdNotLoading => !PwdLoading;
    public bool AvatarNotLoading => !AvatarLoading;

    public string AvatarFileName
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string? SelectedAvatarPath
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string GameInstallPath => _updater.InstallPath;

    public ReactiveCommand<Unit, Unit> ChangePasswordCommand { get; }
    public ReactiveCommand<string, Unit> UpdateAvatarCommand { get; }
    public ReactiveCommand<SolidColorBrush, Unit> ApplyAccentColorCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseAvatarCommand { get; }
    public ReactiveCommand<Unit, Unit> UploadSelectedAvatarCommand { get; }
    public ReactiveCommand<Unit, Unit> ChooseInstallFolderCommand { get; }

    /// <summary>Fired after a successful password change — the View clears PasswordBoxes.</summary>
    public event Action? PasswordChangedSuccessfully;

    private void SyncTheme()
    {
        _isLight = ThemeManager.Current.ApplicationTheme != ApplicationTheme.Dark;
        this.RaisePropertyChanged(nameof(IsLight));
    }

    private void SyncAccent()
    {
        _updatingAccentColor = true;
        AccentColor = ThemeManager.Current.AccentColor;
        _updatingAccentColor = false;

        if (ThemeManager.Current.AccentColor is { } c)
        {
            AccentService.Apply(c);
            _preferences.SetAccentColor(c);
        }
    }

    private async Task ChangePasswordAsync()
    {
        PwdSuccess = false;
        PwdError = false;

        if (!InputValidator.ValidatePassword(OldPassword).IsValid)
        {
            PwdMessage = "Veuillez remplir tous les champs.";
            PwdError = true;
            return;
        }

        var check = InputValidator.ValidatePasswordConfirm(NewPassword, ConfirmPassword);
        if (!check.IsValid)
        {
            PwdMessage = check.ErrorMessage!;
            PwdError = true;
            return;
        }

        if (_auth.Token is null)
        {
            return;
        }

        PwdLoading = true;
        try
        {
            var resp = await _profile.ChangePasswordAsync(_auth.Token, OldPassword, NewPassword);
            if (resp.Success)
            {
                PwdMessage = "Mot de passe modifié avec succès.";
                PwdSuccess = true;
                OldPassword = NewPassword = ConfirmPassword = string.Empty;
                PasswordChangedSuccessfully?.Invoke();
            }
            else
            {
                PwdMessage = resp.ErrorMessage ?? "Erreur inconnue.";
                PwdError = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SettingsViewModel.ChangePassword error");
        }
        finally
        {
            PwdLoading = false;
        }
    }

    public Task ExecuteUpdateAvatarAsync(string filePath)
    {
        return UpdateAvatarAsync(filePath);
    }

    private async Task UpdateAvatarAsync(string filePath)
    {
        AvatarSuccess = false;
        AvatarError = false;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            AvatarMessage = "Aucun fichier sélectionné.";
            AvatarError = true;
            return;
        }

        if (_auth.Token is null)
        {
            return;
        }

        AvatarLoading = true;
        try
        {
            var resp = await _profile.UploadAvatarAsync(_auth.Token, filePath);
            if (resp.Success)
            {
                var url = resp.AvatarUrl ?? string.Empty;
                AvatarUrl = url;
                AvatarMessage = "Avatar mis à jour !";
                AvatarSuccess = true;

                if (_auth.CurrentUser is not null)
                {
                    _auth.CurrentUser.AvatarUrl = url;
                }

                _auth.NotifyAvatarChanged(url);
            }
            else
            {
                AvatarMessage = resp.ErrorMessage ?? "Erreur inconnue.";
                AvatarError = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SettingsViewModel.UpdateAvatar error");
        }
        finally
        {
            AvatarLoading = false;
        }
    }
}