using System.Collections.ObjectModel;
using System.Reactive;
using Minark.Client.Helpers;
using Minark.Client.Services;
using Minark.Client.Services.Interfaces;
using Minark.Shared;
using Minark.Shared.Packets;
using Minark.Shared.Packets.Friends;
using ReactiveUI;
using Serilog;

// ReSharper disable EventUnsubscriptionViaAnonymousDelegate

namespace Minark.Client.ViewModels.Pages;

public class FriendsViewModel : ViewModelBase
{
    private readonly IAuthClientService _authService;
    private readonly NotificationBadgeService _badge;
    private readonly IFriendClientService _friendService;
    private readonly IProfileClientService _profileService;
    private readonly UserStatusService _statusSvc;

    public FriendsViewModel(
        IFriendClientService friendService,
        IAuthClientService authService,
        IProfileClientService profileService,
        UserStatusService statusSvc,
        NotificationBadgeService badge)
    {
        _friendService = friendService;
        _authService = authService;
        _profileService = profileService;
        _statusSvc = statusSvc;
        _badge = badge;

        statusSvc.WhenAnyValue(x => x.Status).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(CurrentStatus));
            this.RaisePropertyChanged(nameof(StatusText));
        });

        friendService.OnFriendListChanged += OnFriendListChangedReceived;
        authService.AvatarChanged += url => UiThread.Invoke(() =>
        {
            AvatarUrl = string.Empty;
            AvatarUrl = url;
        });
        friendService.OnInviteReceived += OnInviteReceivedPush;
        friendService.OnFriendStatusChanged += OnFriendStatusChangedPush;

        var user = authService.CurrentUser;
        if (user is not null)
        {
            Username = user.Username;
            Initials = user.Username.ToInitials();
            AvatarUrl = user.AvatarUrl ?? string.Empty;
        }

        // Derived properties — updated reactively from collection changes
        FriendsList.WhenCollectionChanged().Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(HasFriends));
            this.RaisePropertyChanged(nameof(HasOnlineFriends));
            this.RaisePropertyChanged(nameof(HasOfflineFriends));
        });
        PendingInvites.WhenCollectionChanged().Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(HasPendingInvites));
            this.RaisePropertyChanged(nameof(PendingCount));
        });
        BlockedUsers.WhenCollectionChanged().Subscribe(_ =>
            this.RaisePropertyChanged(nameof(HasBlockedUsers)));
        OnlineFriends.WhenCollectionChanged().Subscribe(_ =>
            this.RaisePropertyChanged(nameof(OnlineCount)));

        LoadFriendsCommand = ReactiveCommand.CreateFromTask(LoadFriendsAsync);
        AddFriendCommand = ReactiveCommand.CreateFromTask(AddFriendAsync,
            this.WhenAnyValue(x => x.SearchUsername, x => x.IsLoading,
                (u, loading) => !string.IsNullOrWhiteSpace(u) && !loading));
        CloseAddPanelCommand = ReactiveCommand.Create(CloseAddPanel);
        ToggleSearchCommand = ReactiveCommand.Create(ToggleSearch);
        ToggleAddPanelCommand = ReactiveCommand.Create(() => { IsAddPanelVisible = !IsAddPanelVisible; });
        AcceptInviteCommand = ReactiveCommand.CreateFromTask<PendingInviteDto>(AcceptInviteAsync);
        DeclineInviteCommand = ReactiveCommand.CreateFromTask<PendingInviteDto>(DeclineInviteAsync);
        RemoveFriendCommand = ReactiveCommand.CreateFromTask<FriendDto>(RemoveFriendAsync);
        BlockFriendCommand = ReactiveCommand.CreateFromTask<FriendDto>(BlockFriendAsync);
        UnblockUserCommand = ReactiveCommand.CreateFromTask<string>(UnblockUserAsync);
        ChangeStatusCommand = ReactiveCommand.CreateFromTask<UserStatus>(ChangeStatusAsync);
        OpenChatCommand = ReactiveCommand.Create<FriendDto>(OpenChat);

        LoadFriendsCommand.ThrownExceptions.Subscribe(ex =>
        {
            IsLoading = false;
            Log.Error(ex, "FriendsViewModel.LoadFriends error");
        });
        AddFriendCommand.ThrownExceptions.Subscribe(ex =>
        {
            IsLoading = false;
            Log.Error(ex, "FriendsViewModel.AddFriend error");
        });
    }

    // ── Collections ───────────────────────────────────────────────────────────

    public ObservableCollection<FriendDto> FriendsList { get; } = [];
    public ObservableCollection<PendingInviteDto> PendingInvites { get; } = [];
    public ObservableCollection<FriendItemViewModel> OnlineFriends { get; } = [];
    public ObservableCollection<FriendItemViewModel> OfflineFriends { get; } = [];
    public ObservableCollection<string> BlockedUsers { get; } = [];

    // ── Properties ────────────────────────────────────────────────────────────

    public string AvatarUrl
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Username
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Initials
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "?";

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasNotification
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsNotificationSuccess
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public string NotificationMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string SearchUsername
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool IsSearchVisible
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsAddPanelVisible
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    // Computed
    public bool HasFriends => FriendsList.Count > 0;
    public bool HasOnlineFriends => OnlineFriends.Count > 0;
    public bool HasOfflineFriends => OfflineFriends.Count > 0;
    public bool HasPendingInvites => PendingInvites.Count > 0;
    public int PendingCount => PendingInvites.Count;
    public int OnlineCount => OnlineFriends.Count;
    public bool HasBlockedUsers => BlockedUsers.Count > 0;

    public UserStatus CurrentStatus
    {
        get => _statusSvc.Status;
        set => _statusSvc.Status = value;
    }

    public string StatusText => _statusSvc.StatusText;

    // ── Commands ──────────────────────────────────────────────────────────────

    public ReactiveCommand<Unit, Unit> LoadFriendsCommand { get; }
    public ReactiveCommand<Unit, Unit> AddFriendCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseAddPanelCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSearchCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleAddPanelCommand { get; }
    public ReactiveCommand<PendingInviteDto, Unit> AcceptInviteCommand { get; }
    public ReactiveCommand<PendingInviteDto, Unit> DeclineInviteCommand { get; }
    public ReactiveCommand<FriendDto, Unit> RemoveFriendCommand { get; }
    public ReactiveCommand<string, Unit> UnblockUserCommand { get; }
    public ReactiveCommand<FriendDto, Unit> BlockFriendCommand { get; }
    public ReactiveCommand<UserStatus, Unit> ChangeStatusCommand { get; }
    public ReactiveCommand<FriendDto, Unit> OpenChatCommand { get; }

    public event Action<FriendDto>? OpenChatRequested;

    // ── Push handlers ─────────────────────────────────────────────────────────

    private void OnFriendListChangedReceived(FriendListChanged changed)
    {
        UiThread.Post(async () =>
        {
            var msg = changed.Reason == "added"
                ? $"{changed.OtherUsername} est maintenant votre ami !"
                : $"{changed.OtherUsername} a été retiré de votre liste d'amis.";
            ShowNotification(msg, changed.Reason == "added");
            await LoadFriendsAsync();
        });
    }

    private void OnInviteReceivedPush(FriendInviteReceived invite)
    {
        UiThread.Invoke(() =>
        {
            if (PendingInvites.Any(p => p.FriendshipId == invite.FriendshipId))
            {
                return;
            }

            PendingInvites.Add(new PendingInviteDto
            {
                FriendshipId = invite.FriendshipId,
                FromUsername = invite.FromUsername,
                SentAt = DateTime.UtcNow
            });
        });
    }

    private void OnFriendStatusChangedPush(FriendStatusUpdate update)
    {
        var me = _authService.CurrentUser?.Username;
        if (!string.IsNullOrEmpty(me) && string.Equals(update.Username, me, StringComparison.OrdinalIgnoreCase))
        {
            Log.Warning("FriendsViewModel: ignoring self-targeted status update ({Username})", update.Username);
            return;
        }

        UiThread.Invoke(() =>
        {
            var friend = FriendsList.FirstOrDefault(f => f.Username == update.Username);
            if (friend is null)
            {
                Log.Information("FriendsViewModel: Friend {Username} not in local list — triggering refresh", update.Username);
                _ = LoadFriendsAsync();
                return;
            }

            var idx = FriendsList.IndexOf(friend);
            FriendsList[idx] = new FriendDto
            {
                Id = friend.Id,
                Username = friend.Username,
                Status = update.Status,
                AvatarUrl = friend.AvatarUrl,
                FriendsSince = friend.FriendsSince
            };
            RebuildFilteredLists();
        });
    }

    // ── Commands implementations ──────────────────────────────────────────────

    public async Task LoadFriendsAsync()
    {
        if (_authService.Token is null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var friendsTask = _friendService.GetFriendsAsync(_authService.Token);
            var blockedTask = _profileService.GetBlockListAsync(_authService.Token);
            await Task.WhenAll(friendsTask, blockedTask);

            var response = friendsTask.Result;
            FriendsList.Clear();
            foreach (var f in response.Friends)
            {
                FriendsList.Add(f);
            }

            PendingInvites.Clear();
            foreach (var p in response.PendingInvites)
            {
                PendingInvites.Add(p);
            }

            BlockedUsers.Clear();
            if (blockedTask.Result.Success)
            {
                foreach (var u in blockedTask.Result.BlockedUsernames)
                {
                    BlockedUsers.Add(u);
                }
            }

            RebuildFilteredLists();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FriendsViewModel.LoadFriends error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CloseAddPanel()
    {
        SearchUsername = string.Empty;
        IsAddPanelVisible = false;
    }

    private void ToggleSearch()
    {
        IsSearchVisible = !IsSearchVisible;
        if (!IsSearchVisible)
        {
            SearchUsername = string.Empty;
        }
    }

    private async Task AddFriendAsync()
    {
        if (_authService.Token is null)
        {
            return;
        }

        var check = InputValidator.ValidateUsername(SearchUsername);
        if (!check.IsValid)
        {
            ShowNotification(check.ErrorMessage!, false);
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _friendService.SendFriendRequestAsync(_authService.Token, SearchUsername.Trim());
            ShowNotification(
                result.Success ? $"Invitation envoyée à {SearchUsername} !" : result.ErrorMessage ?? "Erreur.",
                result.Success);
            if (result.Success)
            {
                SearchUsername = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FriendsViewModel.AddFriend error");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AcceptInviteAsync(PendingInviteDto invite)
    {
        if (_authService.Token is null)
        {
            return;
        }

        await _friendService.AcceptInviteAsync(_authService.Token, invite.FriendshipId);
    }

    private async Task DeclineInviteAsync(PendingInviteDto invite)
    {
        if (_authService.Token is null)
        {
            return;
        }

        await _friendService.DeclineInviteAsync(_authService.Token, invite.FriendshipId);
        PendingInvites.Remove(invite);
    }

    private async Task RemoveFriendAsync(FriendDto friend)
    {
        if (_authService.Token is null)
        {
            return;
        }

        await _friendService.RemoveFriendAsync(_authService.Token, friend.Username);
    }

    private async Task BlockFriendAsync(FriendDto friend)
    {
        if (_authService.Token is null)
        {
            return;
        }

        var resp = await _profileService.BlockUserAsync(_authService.Token, friend.Username);
        if (resp.Success)
        {
            ShowNotification($"{friend.Username} a été bloqué.", true);
            await LoadFriendsAsync();
        }
    }

    private async Task UnblockUserAsync(string username)
    {
        if (_authService.Token is null)
        {
            return;
        }

        var resp = await _profileService.UnblockUserAsync(_authService.Token, username);
        if (resp.Success)
        {
            ShowNotification($"{username} a été débloqué.", true);
            await LoadFriendsAsync();
        }
    }

    private async Task ChangeStatusAsync(UserStatus status)
    {
        if (_authService.Token is null)
        {
            return;
        }

        _statusSvc.Status = status;
        await _friendService.UpdateStatusAsync(_authService.Token, status);
    }

    private void OpenChat(FriendDto friend)
    {
        _badge.ClearFrom(friend.Username);
        OpenChatRequested?.Invoke(friend);
    }

    public void RefreshProfile()
    {
        var user = _authService.CurrentUser;
        if (user is null)
        {
            return;
        }

        Username = user.Username;
        Initials = user.Username.ToInitials();
        AvatarUrl = user.AvatarUrl ?? string.Empty;
        SearchUsername = string.Empty;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RebuildFilteredLists()
    {
        foreach (var item in OnlineFriends)
        {
            item.Dispose();
        }

        foreach (var item in OfflineFriends)
        {
            item.Dispose();
        }

        OnlineFriends.Clear();
        OfflineFriends.Clear();

        foreach (var f in FriendsList)
        {
            var item = new FriendItemViewModel(f, _badge);
            (f.Status == UserStatus.Offline ? OfflineFriends : OnlineFriends).Add(item);
        }

        this.RaisePropertyChanged(nameof(HasOnlineFriends));
        this.RaisePropertyChanged(nameof(HasOfflineFriends));
    }

    private void ShowNotification(string message, bool isSuccess)
    {
        NotificationMessage = message;
        IsNotificationSuccess = isSuccess;
        HasNotification = true;
        Task.Delay(4000).ContinueWith(_ => UiThread.Invoke(() => HasNotification = false));
    }
}