using System.Windows.Controls;
using Minark.Client.Services;
using Minark.Client.Services.Interfaces;
using Minark.Client.ViewModels.Pages;
using Minark.Client.Views.Shared;
using Minark.Client.Views.Windows;

namespace Minark.Client.Views.Pages;

public partial class ShellView
{
    private readonly IServiceProvider _sp;

    public ShellView(ShellViewModel vm, IServiceProvider sp)
    {
        InitializeComponent();
        DataContext = vm;
        _sp = sp;

        // Réagit à ActiveSection (ex. depuis HomeView "Voir tout")
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ShellViewModel.ActiveSection))
            {
                NavigateTo(vm.ActiveSection);
            }
        };

        // Ouvrir la FriendsWindow (logique Window = responsabilité de la View)
        vm.OpenFriendsRequested += async () =>
        {
            var friendsWindow = sp.GetRequiredService<FriendsWindow>();
            await friendsWindow.OpenAsync();
        };

        NavigateTo("Dashboard");

        Loaded += async (_, _) =>
        {
            var auth = sp.GetRequiredService<IAuthClientService>();
            var chat = sp.GetRequiredService<IChatClientService>();
            var badge = sp.GetRequiredService<NotificationBadgeService>();
            if (auth.Token is not null)
            {
                await badge.LoadFromServerAsync(auth.Token, chat);
            }
        };
    }

    public void NavigateToArticle(NewsArticleViewModel article)
    {
        var page = new NewsDetailView(article, NavFrame);
        NavFrame.Navigate(page);
    }

    private void NavigateTo(string? tag)
    {
        var page = tag switch
        {
            "Dashboard" => _sp.GetService(typeof(HomeView)) as UserControl,
            "News" => _sp.GetService(typeof(NewsView)) as UserControl,
            "Library" => _sp.GetService(typeof(LibraryView)) as UserControl,
            "Friends" => _sp.GetService(typeof(FriendsView)) as UserControl,
            "Settings" => _sp.GetService(typeof(SettingsView)) as UserControl,
            "Downloads" => _sp.GetService(typeof(DownloadView)) as UserControl,
            _ => null
        };
        if (page is not null)
        {
            NavFrame.Navigate(page);
        }
    }
}