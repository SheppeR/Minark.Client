using System.Reactive.Disposables;
using Minark.Client.Helpers;
using Minark.Client.Services;
using Minark.Shared.Packets;
using ReactiveUI;

namespace Minark.Client.ViewModels;

/// <summary>
///     Wrapper réactif autour de FriendDto.
///     Implémente IDisposable pour libérer la subscription ReactiveUI.
/// </summary>
public class FriendItemViewModel : ReactiveObject, IDisposable
{
    private readonly IDisposable _badgeSubscription;

    public FriendItemViewModel(FriendDto dto, NotificationBadgeService badge)
    {
        Dto = dto;
        HasUnread = badge.HasUnreadFrom(dto.Username);

        // Subscription via event standard — on wrape dans IDisposable
        void OnUnreadChanged(string username)
        {
            if (username == Dto.Username)
            {
                HasUnread = badge.HasUnreadFrom(username);
            }
        }

        badge.UnreadPerFriendChanged += OnUnreadChanged;
        _badgeSubscription = Disposable.Create(() => badge.UnreadPerFriendChanged -= OnUnreadChanged);
    }

    public FriendDto Dto { get; }
    public string Username => Dto.Username;
    public UserStatus Status => Dto.Status;
    public string? AvatarUrl => Dto.AvatarUrl;
    public string Initials => Username.ToInitials();

    public bool HasUnread
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public void Dispose()
    {
        _badgeSubscription.Dispose();
    }
}