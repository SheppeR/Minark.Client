using System.Reactive;
using Minark.Client.Helpers;
using Minark.Client.Services.Interfaces;
using Minark.Shared.Packets.Friends;
using ReactiveUI;

namespace Minark.Client.ViewModels;

public class InviteNotificationViewModel : ReactiveObject
{
    private readonly IAuthClientService _auth;
    private readonly IFriendClientService _friends;
    private FriendInviteReceived? _invite;

    public InviteNotificationViewModel(IFriendClientService friends, IAuthClientService auth)
    {
        _friends = friends;
        _auth = auth;

        AcceptCommand = ReactiveCommand.CreateFromTask(AcceptAsync);
        DeclineCommand = ReactiveCommand.CreateFromTask(DeclineAsync);
    }

    public string FromUsername
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public string Initials
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = "?";

    public ReactiveCommand<Unit, Unit> AcceptCommand { get; }
    public ReactiveCommand<Unit, Unit> DeclineCommand { get; }

    /// <summary>Déclenché après Accept ou Decline pour que la View se cache.</summary>
    public event Action? CloseRequested;

    public void ShowInvite(FriendInviteReceived invite)
    {
        _invite = invite;
        FromUsername = invite.FromUsername;
        Initials = invite.FromUsername.ToInitials();
    }

    private async Task AcceptAsync()
    {
        if (_invite is not null && _auth.Token is not null)
        {
            await _friends.AcceptInviteAsync(_auth.Token, _invite.FriendshipId);
        }

        CloseRequested?.Invoke();
    }

    private async Task DeclineAsync()
    {
        if (_invite is not null && _auth.Token is not null)
        {
            await _friends.DeclineInviteAsync(_auth.Token, _invite.FriendshipId);
        }

        CloseRequested?.Invoke();
    }
}