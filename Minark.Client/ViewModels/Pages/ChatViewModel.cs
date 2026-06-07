using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Minark.Client.Helpers;
using Minark.Client.Services;
using Minark.Client.Services.Interfaces;
using Minark.Shared.Packets;
using Minark.Shared.Packets.Chat;
using ReactiveUI;
using Serilog;

namespace Minark.Client.ViewModels.Pages;

public class ChatViewModel : ViewModelBase
{
    private readonly IAuthClientService _auth;
    private readonly NotificationBadgeService _badge;
    private readonly IChatClientService _chat;

    // Subscriptions à libérer
    // ReSharper disable once CollectionNeverQueried.Local
    private readonly CompositeDisposable _disposables = new();
    private readonly IFriendClientService _friends;
    private readonly LocalChatHistoryService _history;

    private bool _allLoaded;
    private int _currentPage = 1;

    // ── Typing AFFICHAGE ──────────────────────────────────────────────────────

    // Cancellation token pour reset le timer à chaque nouveau signal
    private IDisposable? _typingDisplaySubscription;

    public ChatViewModel(
        IChatClientService chat,
        IAuthClientService auth,
        LocalChatHistoryService history,
        IFriendClientService friends,
        NotificationBadgeService badge)
    {
        _chat = chat;
        _auth = auth;
        _history = history;
        _friends = friends;
        _badge = badge;

        // ── FriendStatusText dépend de FriendStatus ──────────────────────────
        _disposables.Add(
            this.WhenAnyValue(x => x.FriendStatus)
                .Subscribe(_ => this.RaisePropertyChanged(nameof(FriendStatusText))));

        // ── Typing debounce ENVOI ─────────────────────────────────────────────
        // Quand InputMessage change et n'est pas vide → envoyer TypingStart
        // puis arrêter après 2s d'inactivité
        _disposables.Add(
            this.WhenAnyValue(x => x.InputMessage)
                .Where(_ => _auth.Token is not null && !string.IsNullOrEmpty(FriendUsername))
                .Select(text => !string.IsNullOrWhiteSpace(text))
                .DistinctUntilChanged()
                .Subscribe(isTyping =>
                {
                    _ = isTyping
                        ? _chat.SendTypingStartAsync(FriendUsername)
                        : _chat.SendTypingStopAsync(FriendUsername);
                }));

        // Arrêt automatique du typing après 2s sans frappe
        _disposables.Add(
            this.WhenAnyValue(x => x.InputMessage)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Throttle(TimeSpan.FromSeconds(2))
                .ObserveOn(DispatcherScheduler.Current)
                .Subscribe(__ =>
                {
                    if (_auth.Token is not null && !string.IsNullOrEmpty(FriendUsername))
                    {
                        _ = _chat.SendTypingStopAsync(FriendUsername);
                    }
                }));

        // ── Typing AFFICHAGE ──────────────────────────────────────────────────
        // FriendIsTyping = true disparaît après 4s sans nouveau signal
        _chat.OnTypingStarted += n => OnTyping(n, true);
        _chat.OnTypingStopped += n => OnTyping(n, false);

        // ── Autres events push ────────────────────────────────────────────────
        _chat.OnMessageReceived += OnMessageReceived;
        _chat.OnMessageDeleted += OnMessageDeleted;
        _chat.OnMessageEdited += OnMessageEdited;
        _chat.OnMessageReacted += OnMessageReacted;

        _friends.OnFriendStatusChanged += update =>
        {
            if (update.Username == FriendUsername)
            {
                UiThread.Invoke(() => FriendStatus = update.Status);
            }
        };

        // ── Commandes ─────────────────────────────────────────────────────────
        var hasToken = this.WhenAnyValue(
            x => x.FriendUsername,
            u => !string.IsNullOrEmpty(u) && _auth.Token is not null);

        LoadHistoryCommand = ReactiveCommand.CreateFromTask(LoadHistoryAsync, hasToken);
        LoadMoreCommand = ReactiveCommand.CreateFromTask(LoadMoreAsync,
            this.WhenAnyValue(x => x.IsLoadingMore, x => x.HasMorePages,
                (loading, more) => !loading && more));

        SendCommand = ReactiveCommand.CreateFromTask(SendAsync,
            this.WhenAnyValue(x => x.InputMessage,
                msg => !string.IsNullOrWhiteSpace(msg)));

        DeleteMessageCommand = ReactiveCommand.CreateFromTask<ChatMessageViewModel?>(DeleteMessageAsync);
        StartEditMessageCommand = ReactiveCommand.Create<ChatMessageViewModel?>(StartEditMessage);
        ConfirmEditCommand = ReactiveCommand.CreateFromTask<ChatMessageViewModel?>(ConfirmEditAsync);
        CancelEditCommand = ReactiveCommand.Create<ChatMessageViewModel?>(msg =>
        {
            if (msg is not null)
            {
                msg.IsEditMode = false;
            }
        });
        ToggleReactionPickerCommand = ReactiveCommand.Create<ChatMessageViewModel?>(msg =>
        {
            if (msg is not null)
            {
                msg.ShowReactionPicker = !msg.ShowReactionPicker;
            }
        });
        OpenEmojiPickerForCommand = ReactiveCommand.Create<ChatMessageViewModel?>(msg =>
        {
            if (msg is not null)
            {
                OpenEmojiPickerFor(msg);
            }
        });
        SendReactionCommand =
            ReactiveCommand.CreateFromTask<(ChatMessageViewModel Msg, string Emoji)>(SendReactionAsync);
        ToggleSearchCommand = ReactiveCommand.Create(ToggleSearch);
        SearchCommand = ReactiveCommand.CreateFromTask(SearchAsync,
            this.WhenAnyValue(x => x.SearchQuery,
                q => !string.IsNullOrWhiteSpace(q)));
        ClearSearchCommand = ReactiveCommand.Create(ClearSearch);

        // Éviter que ReactiveUI brise le pipeline en cas d'exception —
        // logger l'erreur et s'assurer que IsLoading/IsLoadingMore sont remis à false
        LoadHistoryCommand.ThrownExceptions
            .Subscribe(ex =>
            {
                IsLoading = false;
                Log.Error(ex, "ChatViewModel.LoadHistory error");
            });
        LoadMoreCommand.ThrownExceptions
            .Subscribe(ex =>
            {
                IsLoadingMore = false;
                Log.Error(ex, "ChatViewModel.LoadMore error");
            });
        SendCommand.ThrownExceptions
            .Subscribe(ex => Log.Error(ex, "ChatViewModel.Send error"));
    }

    // ── Propriétés ────────────────────────────────────────────────────────────

    public string FriendUsername
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string FriendInitials
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = "?";

    public string FriendAvatarUrl
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public UserStatus FriendStatus
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = UserStatus.Offline;

    public bool FriendIsTyping
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasMorePages
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public string InputMessage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsLoadingMore
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsSearchMode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string SearchQuery
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ObservableCollection<ChatMessageViewModel> Messages
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public ObservableCollection<ChatMessageViewModel> SearchResults
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public string FriendStatusText => FriendStatus.ToText();

    // ── Commandes ─────────────────────────────────────────────────────────────
    public ReactiveCommand<Unit, Unit> LoadHistoryCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadMoreCommand { get; }
    public ReactiveCommand<Unit, Unit> SendCommand { get; }
    public ReactiveCommand<ChatMessageViewModel?, Unit> DeleteMessageCommand { get; }
    public ReactiveCommand<ChatMessageViewModel?, Unit> StartEditMessageCommand { get; }
    public ReactiveCommand<ChatMessageViewModel?, Unit> ConfirmEditCommand { get; }
    public ReactiveCommand<ChatMessageViewModel?, Unit> CancelEditCommand { get; }
    public ReactiveCommand<ChatMessageViewModel?, Unit> ToggleReactionPickerCommand { get; }
    public ReactiveCommand<ChatMessageViewModel?, Unit> OpenEmojiPickerForCommand { get; }
    public ReactiveCommand<(ChatMessageViewModel Msg, string Emoji), Unit> SendReactionCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSearchCommand { get; }
    public ReactiveCommand<Unit, Unit> SearchCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearSearchCommand { get; }

    public event Action<string, string>? TrayNotificationRequested;
    public event Action<string>? OpenChatRequested;

    /// <summary>
    ///     Déclenché quand le behavior de clic droit demande à ouvrir l'emoji picker
    ///     pour un message donné. La View ouvre le Popup et appelle
    ///     <see>
    ///         <cref>SetPendingReactionTarget</cref>
    ///     </see>
    ///     .
    /// </summary>
    public event Action<ChatMessageViewModel>? RequestOpenEmojiPicker;

    public void OpenWith(string friendUsername)
    {
        OpenChatRequested?.Invoke(friendUsername);
    }

    /// <summary>
    ///     Appelé par le behavior de clic droit pour déclencher l'ouverture du picker d'emoji.
    ///     La View réagit à <see cref="RequestOpenEmojiPicker" /> et ouvre le Popup.
    /// </summary>
    public void OpenEmojiPickerFor(ChatMessageViewModel msg)
    {
        RequestOpenEmojiPicker?.Invoke(msg);
    }

    // ── Ouvrir une conversation ───────────────────────────────────────────────

    public async Task OpenConversationAsync(
        string friendUsername,
        UserStatus initialStatus = UserStatus.Offline,
        string avatarUrl = "")
    {
        FriendUsername = friendUsername;
        FriendInitials = friendUsername.ToInitials();
        FriendAvatarUrl = avatarUrl;
        FriendStatus = initialStatus;
        FriendIsTyping = false;
        IsSearchMode = false;
        SearchQuery = string.Empty;
        Messages.Clear();
        SearchResults.Clear();
        _currentPage = 1;
        _allLoaded = false;
        HasMorePages = false; // sera mis à true par LoadHistoryAsync si nécessaire

        _badge.ClearFrom(friendUsername);

        var myUsername = _auth.CurrentUser?.Username ?? string.Empty;
        var local = _history.Load(myUsername, friendUsername);
        foreach (var m in local)
        {
            Messages.Add(new ChatMessageViewModel(m));
        }

        await LoadHistoryAsync();
        await MarkAsReadAsync();
    }

    // ── Historique ────────────────────────────────────────────────────────────

    private async Task LoadHistoryAsync()
    {
        if (_auth.Token is null || string.IsNullOrEmpty(FriendUsername))
        {
            return;
        }

        IsLoading = true;
        try
        {
            var response = await _chat.GetHistoryAsync(_auth.Token, FriendUsername);
            if (!response.Success)
            {
                return;
            }

            var myUsername = _auth.CurrentUser?.Username ?? string.Empty;
            _history.Save(myUsername, FriendUsername, response.Messages);

            // ← MERGE les messages serveur avec les locaux
            var merged = _history.Load(myUsername, FriendUsername);
            var limited = merged.TakeLast(10).ToList(); // ← 10 derniers (y compris locaux)
            var newVms = limited.Select(m => new ChatMessageViewModel(m)).ToList();

            UiThread.Invoke(() =>
            {
                // ← NE PAS CLEAR ! Juste remplacer s'il y a des messages serveur
                // Les messages locaux (en attente) sont préservés
                if (newVms.Count > 0 && response.Messages.Count > 0)
                {
                    Messages.Clear();
                    foreach (var vm in newVms)
                    {
                        Messages.Add(vm);
                    }
                }
                // Sinon, on garde les messages locaux qu'on a ajoutés précédemment
            });

            _currentPage = 1;
            _allLoaded = !response.HasMore;
            UiThread.Invoke(() => HasMorePages = response.HasMore);

            Log.Information("ChatViewModel: Loaded {Count} messages (local + server), HasMore: {HasMore}",
                newVms.Count, response.HasMore);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChatViewModel.LoadHistory failed");
        }
        finally
        {
            UiThread.Invoke(() => IsLoading = false);
        }
    }

    private async Task LoadMoreAsync()
    {
        if (_auth.Token is null || _allLoaded)
        {
            return;
        }

        IsLoadingMore = true;
        try
        {
            var nextPage = _currentPage + 1;
            Log.Information("ChatViewModel: Loading page {Page}", nextPage);

            var response = await _chat.GetHistoryAsync(_auth.Token, FriendUsername, nextPage);
            if (!response.Success || response.Messages.Count == 0)
            {
                _allLoaded = true;
                HasMorePages = false;
                Log.Information("ChatViewModel: No more messages");
                return;
            }

            var toInsert = response.Messages
                .Where(m => !Messages.Any(x => x.SentAt == m.SentAt && x.FromUsername == m.FromUsername))
                .Select(m => new ChatMessageViewModel(m))
                .ToList();

            UiThread.Invoke(() =>
            {
                // ← Insérer au DÉBUT (INDEX 0)
                for (var i = toInsert.Count - 1; i >= 0; i--)
                {
                    Messages.Insert(0, toInsert[i]);
                }

                Log.Information("ChatViewModel: Inserted {Count} messages at beginning", toInsert.Count);
            });

            _currentPage = nextPage;
            if (!response.HasMore)
            {
                _allLoaded = true;
                UiThread.Invoke(() => HasMorePages = false);
                Log.Information("ChatViewModel: Reached beginning of conversation");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ChatViewModel.LoadMore failed");
        }
        finally
        {
            UiThread.Invoke(() => IsLoadingMore = false);
        }
    }

    private async Task MarkAsReadAsync()
    {
        if (_auth.Token is null || string.IsNullOrEmpty(FriendUsername))
        {
            return;
        }

        await _chat.MarkAsReadAsync(_auth.Token, FriendUsername);
    }

    // ── Réception messages ────────────────────────────────────────────────────

    private void OnMessageReceived(ChatReceive msg)
    {
        var myUsername = _auth.CurrentUser?.Username ?? string.Empty;
        var isCurrentConv =
            (msg.FromUsername == FriendUsername && msg.ToUsername == myUsername) ||
            (msg.FromUsername == myUsername && msg.ToUsername == FriendUsername);
        if (!isCurrentConv)
        {
            return;
        }

        UiThread.Invoke(() =>
        {
            var dto = new ChatMessageDto
            {
                Id = msg.Id,
                FromUsername = msg.FromUsername,
                Content = msg.Content,
                SentAt = msg.SentAt,
                IsOwn = msg.FromUsername == myUsername
            };

            var alreadyPresent = dto.Id > 0
                ? Messages.Any(m => m.Id == dto.Id)
                : Messages.Any(m => m.SentAt == dto.SentAt &&
                                    m.FromUsername == dto.FromUsername &&
                                    m.Content == dto.Content);
            if (alreadyPresent)
            {
                return;
            }

            var vm = new ChatMessageViewModel(dto);
            Messages.Add(vm);
            _history.Append(myUsername, FriendUsername, dto);

            if (!dto.IsOwn)
            {
                TrayNotificationRequested?.Invoke(msg.FromUsername, msg.Content);
            }
        });
    }

    private void OnMessageDeleted(ChatDeleteNotify notify)
    {
        UiThread.Invoke(() =>
            Messages.FirstOrDefault(m => m.Id == notify.MessageId)?.ApplyDelete());
    }

    private void OnMessageEdited(ChatEditNotify notify)
    {
        UiThread.Invoke(() =>
            Messages.FirstOrDefault(m => m.Id == notify.MessageId)?.ApplyEdit(notify.NewContent));
    }

    private void OnMessageReacted(ChatReactNotify notify)
    {
        UiThread.Invoke(() =>
        {
            var vm = Messages.FirstOrDefault(m => m.Id == notify.MessageId);
            if (vm is null)
            {
                return;
            }

            vm.SyncReactions(notify.Reactions);

            var myUsername = _auth.CurrentUser?.Username ?? string.Empty;
            var cached = _history.Load(myUsername, FriendUsername);
            var msg = cached.FirstOrDefault(m => m.Id == notify.MessageId);
            if (msg is not null)
            {
                msg.Reactions = notify.Reactions;
                _history.Save(myUsername, FriendUsername, cached);
            }
        });
    }

    private void OnTyping(TypingNotification n, bool typing)
    {
        if (n.FromUsername != FriendUsername)
        {
            return;
        }

        UiThread.Invoke(() =>
        {
            _typingDisplaySubscription?.Dispose();
            FriendIsTyping = typing;

            if (typing)
            {
                // Auto-reset après 4s si aucun nouveau signal
                _typingDisplaySubscription = Observable
                    .Timer(TimeSpan.FromSeconds(4))
                    .ObserveOn(DispatcherScheduler.Current)
                    .Subscribe(_ => FriendIsTyping = false);
            }
        });
    }

    // ── Envoi ─────────────────────────────────────────────────────────────────

    private async Task SendAsync()
    {
        var text = InputMessage.Trim();
        if (string.IsNullOrEmpty(text) || _auth.Token is null)
        {
            return;
        }

        InputMessage = string.Empty;

        // Arrêter immédiatement le signal typing
        if (!string.IsNullOrEmpty(FriendUsername))
        {
            await _chat.SendTypingStopAsync(FriendUsername);
        }

        await _chat.SendMessageAsync(_auth.Token, FriendUsername, text);
    }

    // ── Suppression ───────────────────────────────────────────────────────────

    private async Task DeleteMessageAsync(ChatMessageViewModel? msg)
    {
        if (msg is null || !msg.IsOwn || _auth.Token is null)
        {
            return;
        }

        await _chat.DeleteMessageAsync(_auth.Token, msg.Id);
    }

    // ── Édition ───────────────────────────────────────────────────────────────

    private void StartEditMessage(ChatMessageViewModel? msg)
    {
        if (msg is null || !msg.IsOwn || msg.IsDeleted)
        {
            return;
        }

        foreach (var m in Messages)
        {
            m.IsEditMode = false;
        }

        msg.EditBuffer = msg.Content;
        msg.IsEditMode = true;
    }

    private async Task ConfirmEditAsync(ChatMessageViewModel? msg)
    {
        if (msg is null || _auth.Token is null)
        {
            return;
        }

        var newContent = msg.EditBuffer.Trim();
        if (string.IsNullOrEmpty(newContent) || newContent == msg.Content)
        {
            msg.IsEditMode = false;
            return;
        }

        msg.ApplyEdit(newContent);
        if (msg.Id > 0)
        {
            await _chat.EditMessageAsync(_auth.Token, msg.Id, newContent);
        }
    }

    // ── Réactions ─────────────────────────────────────────────────────────────

    private async Task SendReactionAsync((ChatMessageViewModel Msg, string Emoji) param)
    {
        if (_auth.Token is null)
        {
            return;
        }

        param.Msg.ShowReactionPicker = false;
        await _chat.ReactToMessageAsync(_auth.Token, param.Msg.Id, param.Emoji);
    }

    // ── Recherche ─────────────────────────────────────────────────────────────

    private void ToggleSearch()
    {
        IsSearchMode = !IsSearchMode;
        if (!IsSearchMode)
        {
            SearchQuery = string.Empty;
            SearchResults.Clear();
        }
    }

    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return;
        }

        SearchResults.Clear();
        var q = SearchQuery.Trim().ToLower();

        // Résultats locaux
        foreach (var m in Messages.Where(m => !m.IsDeleted && m.Content.ToLower().Contains(q)))
        {
            SearchResults.Add(m);
        }

        // Résultats serveur (messages plus anciens)
        if (_auth.Token is not null)
        {
            var response = await _chat.SearchMessagesAsync(_auth.Token, FriendUsername, SearchQuery);
            if (response.Success)
            {
                foreach (var m in response.Results)
                {
                    if (SearchResults.All(x => x.SentAt != m.SentAt || x.FromUsername != m.FromUsername))
                    {
                        SearchResults.Add(new ChatMessageViewModel(m));
                    }
                }
            }
        }
    }

    private void ClearSearch()
    {
        SearchQuery = string.Empty;
        SearchResults.Clear();
        IsSearchMode = false;
    }
}