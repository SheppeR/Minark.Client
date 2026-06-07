using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Minark.Client.Services;
using Minark.Client.ViewModels;
using Minark.Client.ViewModels.Pages;
using Minark.Shared.Packets;

namespace Minark.Client.Views.Windows;

public partial class ChatWindow
{
    private readonly ChatViewModel _vm;
    private ChatMessageViewModel? _pendingReactionMsg;

    public ChatWindow(ChatViewModel vm, SoundService sound, NotificationService notifications)
    {
        InitializeComponent();
        DataContext = _vm = vm;

        vm.TrayNotificationRequested += (from, body) =>
        {
            sound.PlayMessageReceived();
            notifications.ShowMessage(from, body);
        };

        vm.OpenChatRequested += username =>
            Dispatcher.InvokeAsync(async () => await OpenForAsync(username));

        vm.RequestOpenEmojiPicker += msg =>
        {
            _pendingReactionMsg = msg;
            Dispatcher.Invoke(() => EmojiPopup.IsOpen = true);
        };

        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
    }

    public async Task OpenForAsync(string friendUsername,
        UserStatus status = UserStatus.Offline,
        string avatarUrl = "")
    {
        if (!IsVisible)
        {
            Show();
        }

        Activate();
        await _vm.OpenConversationAsync(friendUsername, status, avatarUrl);
    }

    // ── Visual tree walk — unavoidable for ReactionBadge click ──────────────

    private void ReactionBadge_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ReactionViewModel reaction } element)
        {
            return;
        }

        var msgVm = FindAncestorDataContext<ChatMessageViewModel>(element);
        if (msgVm is null)
        {
            return;
        }

        e.Handled = true;
        _vm.SendReactionCommand.Execute((msgVm, reaction.Emoji)).Subscribe();
    }

    private void EmojiPicker_EmojiSelected(string emoji)
    {
        if (string.IsNullOrEmpty(emoji) || _pendingReactionMsg is null)
        {
            return;
        }

        var msgVm = _pendingReactionMsg;
        _pendingReactionMsg = null;
        EmojiPopup.IsOpen = false;
        _vm.SendReactionCommand.Execute((msgVm, emoji)).Subscribe();
    }

    private static T? FindAncestorDataContext<T>(DependencyObject? element) where T : class
    {
        while (element is not null)
        {
            if (element is FrameworkElement { DataContext: T vm })
            {
                return vm;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }
}