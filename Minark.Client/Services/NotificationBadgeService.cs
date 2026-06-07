using System.Collections.Concurrent;
using System.Reactive.Linq;
using Minark.Client.Helpers;
using Minark.Client.Services.Interfaces;
using ReactiveUI;

namespace Minark.Client.Services;

/// <summary>
///     Compte les notifications non lues globalement (badge bouton Amis)
///     et par ami (point rouge sur chaque item de la liste).
/// </summary>
public class NotificationBadgeService : ReactiveObject
{
    private readonly ObservableAsPropertyHelper<string> _badgeText;
    private readonly ObservableAsPropertyHelper<bool> _hasBadge;

    private readonly ObservableAsPropertyHelper<int> _totalCount;
    private readonly ConcurrentDictionary<string, int> _unreadPerFriend = new();

    public NotificationBadgeService(
        IFriendClientService friends,
        IChatClientService chat,
        IAuthClientService auth)
    {
        // TotalCount, HasBadge, BadgeText sont des computed dérivés de UnreadMessages + PendingInvites
        var total = this.WhenAnyValue(
            x => x.UnreadMessages,
            x => x.PendingInvites,
            (u, p) => u + p);

        total.ToProperty(this, x => x.TotalCount, out _totalCount);
        total.Select(t => t > 0).ToProperty(this, x => x.HasBadge, out _hasBadge);
        total.Select(t => t > 99 ? "99+" : t.ToString()).ToProperty(this, x => x.BadgeText, out _badgeText);

        friends.OnInviteReceived += _ =>
            UiThread.Invoke(() => PendingInvites++);

        chat.OnMessageReceived += msg =>
        {
            var me = auth.CurrentUser?.Username ?? string.Empty;
            if (msg.FromUsername == me)
            {
                return;
            }

            UiThread.Invoke(() =>
            {
                UnreadMessages++;
                _unreadPerFriend.AddOrUpdate(msg.FromUsername, 1, (_, c) => c + 1);
                UnreadPerFriendChanged?.Invoke(msg.FromUsername);
            });
        };
    }

    public int UnreadMessages
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int PendingInvites
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int TotalCount => _totalCount.Value;
    public bool HasBadge => _hasBadge.Value;
    public string BadgeText => _badgeText.Value;

    /// <summary>Déclenché quand le compteur d'un ami change.</summary>
    public event Action<string>? UnreadPerFriendChanged;

    public bool HasUnreadFrom(string username)
    {
        return _unreadPerFriend.TryGetValue(username, out var c) && c > 0;
    }

    public void ClearFrom(string username)
    {
        if (_unreadPerFriend.TryRemove(username, out var count))
        {
            UnreadMessages = Math.Max(0, UnreadMessages - count);
            UnreadPerFriendChanged?.Invoke(username);
        }
    }

    public void ClearAll()
    {
        UnreadMessages = 0;
        PendingInvites = 0;
        _unreadPerFriend.Clear();
    }

    public void ClearMessages()
    {
        UnreadMessages = 0;
        _unreadPerFriend.Clear();
    }

    public void ClearInvites()
    {
        PendingInvites = 0;
    }

    public async Task LoadFromServerAsync(string token, IChatClientService chat)
    {
        var resp = await chat.GetUnreadCountsAsync(token);
        if (!resp.Success || resp.Counts.Count == 0)
        {
            return;
        }

        UiThread.Invoke(() =>
        {
            foreach (var (fromUsername, count) in resp.Counts)
            {
                _unreadPerFriend[fromUsername] = count;
                UnreadMessages += count;
                UnreadPerFriendChanged?.Invoke(fromUsername);
            }
        });
    }
}