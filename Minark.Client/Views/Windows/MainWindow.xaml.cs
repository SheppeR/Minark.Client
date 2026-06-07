using Minark.Client.Behaviors;
using Minark.Client.Services;
using Minark.Client.Services.Interfaces;
using Minark.Client.ViewModels;
using Minark.Client.ViewModels.Pages;
using Minark.Client.Views.Pages;

namespace Minark.Client.Views.Windows;

public partial class MainWindow
{
    private readonly INavigationService _nav;

    public MainWindow(
        INavigationService nav,
        MainWindowViewModel vm,
        LoginViewModel loginVm,
        IFriendClientService friends,
        InviteNotificationWindow inviteWindow)
    {
        InitializeComponent();
        DataContext = vm;
        _nav = nav;

        _nav.OnNavigated += view =>
            Dispatcher.Invoke(() => FlipTransitionBehavior.SetContent(RootContent, view));

        _nav.NavigateTo<LoginView>();

        friends.OnInviteReceived += invite =>
            Dispatcher.Invoke(() => inviteWindow.ShowInvite(invite));
    }
}