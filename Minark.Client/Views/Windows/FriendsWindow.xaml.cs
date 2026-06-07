using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Minark.Client.Services;
using Minark.Client.ViewModels;
using Minark.Client.ViewModels.Pages;
using Minark.Client.Views.Pages;

namespace Minark.Client.Views.Windows;

public partial class FriendsWindow
{
    private readonly INavigationService _nav;
    private readonly FriendsViewModel _vm;

    public FriendsWindow(FriendsViewModel vm, ChatWindow chatWindow, INavigationService nav)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _nav = nav;

        vm.OpenChatRequested += friend =>
            Dispatcher.InvokeAsync(async () =>
                await chatWindow.OpenForAsync(friend.Username, friend.Status, friend.AvatarUrl ?? string.Empty));

        Closing += (_, e) =>
        {
            e.Cancel = true;
            Hide();
        };
        Loaded += OnLoaded;
        nav.OnNavigated += view =>
        {
            if (view is LoginView)
            {
                Dispatcher.Invoke(Hide);
            }
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 16;
        Top = screen.Bottom - Height - 16;
    }

    public async Task OpenAsync()
    {
        _vm.RefreshProfile();
        _vm.IsAddPanelVisible = false;
        await _vm.LoadFriendsAsync();
        if (!IsVisible)
        {
            Show();
        }
    }

    // Visual tree walk — unavoidable to find FriendItemViewModel for context menu
    private void FriendList_RightClick(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        FriendItemViewModel? itemVm = null;
        FrameworkElement? hitElement = null;

        while (element is not null)
        {
            if (element is FrameworkElement { DataContext: FriendItemViewModel vm } fe)
            {
                itemVm = vm;
                hitElement = fe;
                break;
            }

            element = VisualTreeHelper.GetParent(element) ?? (element as FrameworkElement)?.Parent;
        }

        if (itemVm is null)
        {
            return;
        }

        if (FindResource("FriendContextMenu") is ContextMenu menu)
        {
            menu.Tag = itemVm.Dto;
            menu.DataContext = _vm;
            menu.PlacementTarget = hitElement;
            menu.Placement = PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        e.Handled = true;
    }
}