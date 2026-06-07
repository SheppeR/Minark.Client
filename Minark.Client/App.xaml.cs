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
    }

    // ── Tray icon ─────────────────────────────────────────────────────────────

    private TaskbarIcon CreateTrayIcon()
    {
        var menu = new ContextMenu();
        var menuOpen = new MenuItem { Header = "Ouvrir Minark", FontWeight = FontWeights.Bold };
        menuOpen.Click += TrayMenu_ShowMain;
        var menuFriends = new MenuItem { Header = "Amis" };
        menuFriends.Click += TrayMenu_ShowFriends;
        var menuQuit = new MenuItem { Header = "Quitter" };
        menuQuit.Click += TrayMenu_Quit;

        menu.Items.Add(menuOpen);
        menu.Items.Add(menuFriends);
        menu.Items.Add(new Separator());
        menu.Items.Add(menuQuit);

        var icon = new TaskbarIcon
        {
            ToolTipText = "Minark",
            ContextMenu = menu,
            Id = Guid.NewGuid(),
            IconSource = new GeneratedIconSource
            {
                Text = "P",
                Foreground = Brushes.White,
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6366F1"))
            }
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