using Minark.Client.Helpers;
using Minark.Client.ViewModels;
using Minark.Client.Views.Windows;

namespace Minark.Client.Views.Pages;

public partial class HomeView
{
    public HomeView(HomeDashboardViewModel vm, FriendsWindow friendsWindow)
    {
        InitializeComponent();
        DataContext = vm;

        vm.ArticleOpenRequested += article =>
            this.TryFindParent<ShellView>()?.NavigateToArticle(article);

        vm.OpenFriendsWindowRequested += async () => await friendsWindow.OpenAsync();

        Loaded += async (_, _) => await vm.LoadAsync();
    }
}