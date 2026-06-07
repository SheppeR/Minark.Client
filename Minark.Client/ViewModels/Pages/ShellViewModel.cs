using System.Reactive;
using Minark.Client.Helpers;
using Minark.Client.Networking;
using Minark.Client.Services;
using Minark.Client.Services.Interfaces;
using Minark.Client.Views.Pages;
using Minark.Shared.Packets;
using ReactiveUI;
using Serilog;

namespace Minark.Client.ViewModels.Pages;

public class ShellViewModel : ViewModelBase
{
    private readonly IAuthClientService _auth;
    private readonly ObservableAsPropertyHelper<string> _badgeText;
    private readonly IFriendClientService _friends;

    private readonly ObservableAsPropertyHelper<bool> _hasBadge;
    private readonly INavigationService _nav;
    private readonly ReconnectionService _reconnection;
    private readonly UserStatusService _statusSvc;
    private readonly ObservableAsPropertyHelper<string> _statusText;
    private readonly TcpClientService _tcp;

    private string _avatarUrl;
    private string _initials;
    private string _username;

    public ShellViewModel(
        IAuthClientService auth,
        INavigationService nav,
        IFriendClientService friends,
        UserStatusService statusSvc,
        TcpClientService tcp,
        ReconnectionService reconnection,
        NotificationBadgeService badge)
    {
        _auth = auth;
        _nav = nav;
        _friends = friends;
        _tcp = tcp;
        _reconnection = reconnection;
        _statusSvc = statusSvc;

        _username = auth.CurrentUser?.Username ?? "Utilisateur";
        _initials = _username.ToInitials();
        _avatarUrl = auth.CurrentUser?.AvatarUrl ?? string.Empty;

        // HasBadge et BadgeText : dérivés directement depuis le service ReactiveObject
        badge.WhenAnyValue(x => x.HasBadge)
            .ToProperty(this, x => x.HasBadge, out _hasBadge);
        badge.WhenAnyValue(x => x.BadgeText)
            .ToProperty(this, x => x.BadgeText, out _badgeText);

        // StatusText : dérivé depuis le service ReactiveObject
        statusSvc.WhenAnyValue(x => x.StatusText)
            .ToProperty(this, x => x.StatusText, out _statusText);

        // CurrentStatus : on expose Status via propriété wrapper + notifie quand ça change
        statusSvc.WhenAnyValue(x => x.Status)
            .Subscribe(_ => this.RaisePropertyChanged(nameof(CurrentStatus)));

        // Envoyer le statut initial au serveur
        _ = Task.Run(async () =>
        {
            if (auth.Token is not null)
            {
                try
                {
                    await friends.UpdateStatusAsync(auth.Token, statusSvc.Status);
                    Log.Information("ShellViewModel: Initial status sent - {Status}", statusSvc.Status);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "ShellViewModel: Failed to send initial status");
                }
            }
        });

        auth.LoggedIn += user =>
        {
            Username = user.Username;
            Initials = user.Username.ToInitials();
            AvatarUrl = user.AvatarUrl ?? string.Empty;
        };

        auth.AvatarChanged += url =>
        {
            AvatarUrl = string.Empty;
            AvatarUrl = url;
        };

        NavigateCommand = ReactiveCommand.Create<string>(tag =>
        {
            if (tag == "Downloads")
            {
                HasUpdateBadge = false; // effacer le badge quand on ouvre la page
            }

            if (tag != ActiveSection)
            {
                ActiveSection = tag;
            }
        });

        OpenFriendsCommand = ReactiveCommand.Create(() => OpenFriendsRequested?.Invoke());
        ChangeStatusCommand = ReactiveCommand.CreateFromTask<UserStatus>(ChangeStatusAsync);
        LogoutCommand = ReactiveCommand.CreateFromTask(LogoutAsync);
    }

    public string ActiveSection
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "Dashboard";

    public bool HasBadge => _hasBadge.Value;
    public string BadgeText => _badgeText.Value;

    public bool HasUpdateBadge
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string Username
    {
        get => _username;
        set => this.RaiseAndSetIfChanged(ref _username, value);
    }

    public string Initials
    {
        get => _initials;
        set => this.RaiseAndSetIfChanged(ref _initials, value);
    }

    public string AvatarUrl
    {
        get => _avatarUrl;
        set => this.RaiseAndSetIfChanged(ref _avatarUrl, value);
    }

    public UserStatus CurrentStatus
    {
        get => _statusSvc.Status;
        set => _statusSvc.Status = value;
    }

    public string StatusText => _statusText.Value;

    public ReactiveCommand<string, Unit> NavigateCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFriendsCommand { get; }
    public ReactiveCommand<UserStatus, Unit> ChangeStatusCommand { get; }
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }

    public event Action? OpenFriendsRequested;

    private async Task ChangeStatusAsync(UserStatus status)
    {
        if (_auth.Token is null)
        {
            return;
        }

        _statusSvc.Status = status;
        Log.Information("ShellViewModel: User changed status to {Status}", status);
        await _friends.UpdateStatusAsync(_auth.Token, status);
    }

    private async Task LogoutAsync()
    {
        Log.Information("ShellViewModel: Logging out user {Username}", _username);
        _reconnection.NotifyUserLoggedOut();
        _tcp.Disconnect();
        await _auth.LogoutAsync();
        _nav.NavigateTo<LoginView>();
    }
}