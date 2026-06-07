using System.Windows;
using Minark.Client.ViewModels;
using Minark.Shared.Packets.Friends;

namespace Minark.Client.Views.Windows;

public partial class InviteNotificationWindow
{
    private readonly InviteNotificationViewModel _vm;

    public InviteNotificationWindow(InviteNotificationViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;

        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 16;
        Top = area.Bottom - Height - 76;

        _vm.CloseRequested += () => Dispatcher.Invoke(Hide);
    }

    public void ShowInvite(FriendInviteReceived invite)
    {
        _vm.ShowInvite(invite);
        Show();
        Activate();
    }
}