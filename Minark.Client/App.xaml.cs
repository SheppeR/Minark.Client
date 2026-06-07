using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using H.NotifyIcon;
using iNKORE.UI.WPF.Modern;
using Microsoft.Extensions.Configuration;
using Minark.Client.Networking;
using Minark.Client.Services;
using Minark.Client.Services.Interfaces;
using Minark.Client.ViewModels;
using Minark.Client.ViewModels.Pages;
using Minark.Client.Views.Pages;
using Minark.Client.Views.Shared;
using Minark.Client.Views.Windows;
using Minark.Shared;
using ReactiveUI.Builder;
using Serilog;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace Minark.Client;

public partial class App
{
    private IHost? _host;
    private TaskbarIcon? _trayIcon;

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            RxAppBuilder.CreateReactiveUIBuilder().WithWpf().BuildApp();
            SetCurrentProcessExplicitAppUserModelID($"Minark.Client.{Guid.NewGuid():N}");
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var profileService = new ProfileService();
            Log.Logger = SerilogUtils.SetupClient(profileService.GetSubDirectory("logs"));
            SerilogUtils.PrintSection("MINARK CLIENT");
            Log.Information("Profile: {Name} (bootstrap={IsBootstrap})", profileService.Name, profileService.IsBootstrap);

            _host = BuildHost(profileService);
            await _host.StartAsync();

            InitializeServices(profileService);
            InitializeTrayIcon();

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            mainWindow.Closing += (_, args) =>
            {
                args.Cancel = true;
                mainWindow.Hide(true);
            };
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Erreur au démarrage :\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.InnerException?.Message}\n\n{ex.StackTrace}",
                "Minark - Erreur fatale", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static IHost BuildHost(ProfileService profileService)
    {
        return Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((ctx, services) =>
            {
                services.AddSingleton(profileService);

                var host = ctx.Configuration["Server:Host"] ?? "127.0.0.1";
                var port = int.Parse(ctx.Configuration["Server:Port"] ?? "9000");

                services.AddSingleton(sp => new TcpClientService(host, port, sp.GetRequiredService<ILogger<TcpClientService>>()));
                services.AddSingleton<PacketDispatcher>();

                services.AddSingleton<IAuthClientService, AuthClientService>();
                services.AddSingleton<IFriendClientService, FriendClientService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<INewsClientService, NewsClientService>();
                services.AddSingleton<IChatClientService, ChatClientService>();
                services.AddSingleton<ICredentialsService, CredentialsService>();
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<UserStatusService>();
                services.AddSingleton<LocalChatHistoryService>();
                services.AddSingleton<NotificationBadgeService>();
                services.AddSingleton<IProfileClientService, ProfileClientService>();
                services.AddSingleton<PreferencesService>();
                services.AddSingleton<SoundService>();
                services.AddSingleton<NotificationService>();
                services.AddSingleton<ReconnectionService>();
                services.AddSingleton<IGameLauncherService, GameLauncherService>();
                services.AddSingleton<IGameUpdaterService, GameUpdaterService>();

                services.AddTransient<LoginViewModel>();
                services.AddTransient<RegisterViewModel>();
                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<FriendsViewModel>();
                services.AddSingleton<ChatViewModel>();
                services.AddSingleton<NewsViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddSingleton<LibraryViewModel>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<InviteNotificationViewModel>();
                services.AddTransient<HomeDashboardViewModel>();

                services.AddSingleton<MainWindow>();
                services.AddTransient<LoginView>();
                services.AddTransient<RegisterView>();
                services.AddTransient<ShellView>();
                services.AddTransient<HomeView>();
                services.AddSingleton<LibraryView>();
                services.AddSingleton<DownloadView>();
                services.AddTransient<FriendsView>();
                services.AddTransient<SettingsView>();
                services.AddTransient<NewsView>();
                services.AddSingleton<FriendsWindow>();
                services.AddSingleton<ChatWindow>();
                services.AddSingleton<InviteNotificationWindow>();
            })
            .Build();
    }

    private void InitializeServices(ProfileService profileService)
    {
        _host!.Services.GetRequiredService<ReconnectionService>();

        var prefs = _host.Services.GetRequiredService<PreferencesService>();

        void ApplyUserPreferences()
        {
            Dispatcher.Invoke(() =>
            {
                ThemeService.Initialize(prefs.GetTheme());
                var accent = prefs.GetAccentColor() ?? Color.FromRgb(0x7C, 0x3A, 0xED);
                ThemeManager.Current.AccentColor = accent;
                AccentService.Apply(accent);
            });
        }

        profileService.ProfileSwitched += ApplyUserPreferences;
        ApplyUserPreferences();

        WebConfig.Init(_host.Services.GetRequiredService<IConfiguration>());

        var tcp = _host.Services.GetRequiredService<TcpClientService>();
        var dispatcher = _host.Services.GetRequiredService<PacketDispatcher>();
        tcp.OnDataReceived += dispatcher.Dispatch;
        _ = Task.Run(() => tcp.ConnectAsync()).ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Log.Error(t.Exception, "ConnectAsync failed");
            }
        });

        // Vérifier les mises à jour après le login (quand le vrai profil est chargé)
        // Le profil bootstrap au démarrage n'a pas les préférences → InstallPath serait faux
        profileService.ProfileSwitched += () =>
        {
            _ = Task.Run(async () =>
            {
                var updater = _host.Services.GetRequiredService<IGameUpdaterService>();
                var shellVm = _host.Services.GetRequiredService<ShellViewModel>();

                // Petit délai pour laisser PreferencesService recharger ses prefs
                await Task.Delay(500);

                if (updater.InstalledVersion is null)
                {
                    Log.Information("App: jeu non installé (InstallPath={P}), pas de check MAJ",
                        updater.InstallPath);
                    return;
                }

                Log.Information("App: check MAJ après login (installPath={P}, version={V})...",
                    updater.InstallPath, updater.InstalledVersion);

                try
                {
                    var result = await updater.CheckForUpdatesAsync();
                    if (result is { UpdateAvailable: true })
                    {
                        Log.Information("App: mise à jour disponible — local={Local} remote={Remote}",
                            updater.InstalledVersion, result.RemoteVersion);
                        Dispatcher.Invoke(() => shellVm.HasUpdateBadge = true);
                    }
                    else if (result is not null)
                    {
                        Log.Information("App: jeu à jour — {V}", updater.InstalledVersion);
                    }
                    else
                    {
                        Log.Warning("App: check MAJ retourné null après login");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "App: check MAJ échoué après login");
                }
            });
        };

        var auth = _host.Services.GetRequiredService<IAuthClientService>();
        var creds = _host.Services.GetRequiredService<ICredentialsService>();
        var nav = _host.Services.GetRequiredService<INavigationService>();
        var reconnection = _host.Services.GetRequiredService<ReconnectionService>();

        auth.SessionInvalidated += reason => Dispatcher.Invoke(() =>
        {
            reconnection.NotifyUserLoggedOut();
            creds.Clear();
            nav.NavigateTo<LoginView>();
            MessageBox.Show(reason, "Session terminée", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        nav.OnNavigated += view =>
        {
            if (_trayIcon is null)
            {
                return;
            }

            _trayIcon.ToolTipText = view is ShellView && !string.IsNullOrEmpty(auth.CurrentUser?.Username)
                ? $"Minark — {auth.CurrentUser.Username}"
                : "Minark";
        };
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = CreateTrayIcon();

        var notifSvc = _host!.Services.GetRequiredService<NotificationService>();
        notifSvc.ShowTrayBalloon = (title, message) => _trayIcon.TrayIcon.ShowNotification(title, message);

        // H.NotifyIcon requires explicit disposal before the process exits to remove
        // the tray icon from the shell notification area. The Exit event fires
        // synchronously during Shutdown(), before the process terminates, making it
        // the most reliable hook — OnExit() is async and may not complete in time.
        Exit += (_, _) =>
        {
            _trayIcon?.Dispose();
            _trayIcon = null;
        };
    }

    // ── Tray icon ─────────────────────────────────────────────────────────────

    private static Icon LoadTrayIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "minark.ico");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"minark.ico introuvable dans {AppContext.BaseDirectory}", path);
        }

        return new Icon(path);
    }

    /// <summary>Crée une icône Segoe MDL2 Assets pour un MenuItem du tray.</summary>
    private static TextBlock TrayMenuIcon(string glyph, Brush? foreground = null)
    {
        return new TextBlock
        {
            Text = glyph,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = foreground ?? new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED))
        };
    }

    private TaskbarIcon CreateTrayIcon()
    {
        var menuOpen = new MenuItem
        {
            Header = "Ouvrir Minark",
            FontWeight = FontWeights.Bold,
            Icon = TrayMenuIcon("\uE737"), // Home
            Visibility = Visibility.Collapsed
        };
        menuOpen.Click += TrayMenu_ShowMain;

        var menuFriends = new MenuItem
        {
            Header = "Amis",
            Icon = TrayMenuIcon("\uE716"), // People
            Visibility = Visibility.Collapsed
        };
        menuFriends.Click += TrayMenu_ShowFriends;

        var menuSep = new Separator { Visibility = Visibility.Collapsed };

        var menuQuit = new MenuItem
        {
            Header = "Quitter",
            Foreground = Brushes.IndianRed,
            Icon = TrayMenuIcon("\uE7E8", Brushes.IndianRed) // ChromeClose
        };
        menuQuit.Click += TrayMenu_Quit;

        // Afficher les items connectés dès le login, les masquer à la déconnexion
        var auth = _host!.Services.GetRequiredService<IAuthClientService>();

        void ShowLoggedIn()
        {
            Dispatcher.Invoke(() =>
            {
                menuOpen.Visibility = Visibility.Visible;
                menuFriends.Visibility = Visibility.Visible;
                menuSep.Visibility = Visibility.Visible;
            });
        }

        void HideLoggedOut()
        {
            Dispatcher.Invoke(() =>
            {
                menuOpen.Visibility = Visibility.Collapsed;
                menuFriends.Visibility = Visibility.Collapsed;
                menuSep.Visibility = Visibility.Collapsed;
            });
        }

        auth.LoggedIn += _ => ShowLoggedIn();
        auth.SessionInvalidated += _ => HideLoggedOut();

        // Si déjà connecté (reconnexion automatique), afficher immédiatement
        if (auth.IsLoggedIn)
        {
            ShowLoggedIn();
        }

        var menu = new ContextMenu();
        menu.Items.Add(menuOpen);
        menu.Items.Add(menuFriends);
        menu.Items.Add(menuSep);
        menu.Items.Add(menuQuit);

        var icon = new TaskbarIcon
        {
            ToolTipText = "Minark",
            ContextMenu = menu,
            Id = Guid.NewGuid(),
            Icon = LoadTrayIcon()
        };
        icon.ForceCreate(false);
        icon.TrayMouseDoubleClick += (_, _) => TrayMenu_ShowMain(icon, new RoutedEventArgs());
        return icon;
    }

    private void TrayMenu_ShowMain(object sender, RoutedEventArgs e)
    {
        var mainWindow = _host!.Services.GetRequiredService<MainWindow>();
        mainWindow.Show(true);
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
    }

    private async void TrayMenu_ShowFriends(object sender, RoutedEventArgs e)
    {
        TrayMenu_ShowMain(sender, e);
        await _host!.Services.GetRequiredService<FriendsWindow>().OpenAsync();
    }

    private void TrayMenu_Quit(object sender, RoutedEventArgs e)
    {
        _trayIcon?.Dispose();
        Current.Shutdown();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        await Log.CloseAndFlushAsync();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        base.OnSessionEnding(e);
    }

    ~App()
    {
        _trayIcon?.Dispose();
    }

    public static T GetService<T>() where T : notnull
    {
        return ((App)Current)._host!.Services.GetRequiredService<T>();
    }
}