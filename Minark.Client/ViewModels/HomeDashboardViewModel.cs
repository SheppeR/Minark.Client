using System.Collections.ObjectModel;
using System.Reactive;
using System.Windows.Input;
using Minark.Client.Services.Interfaces;
using Minark.Client.ViewModels.Pages;
using Minark.Shared.Packets;
using ReactiveUI;

namespace Minark.Client.ViewModels;

/// <summary>
///     Aggregates ShellViewModel + NewsViewModel + FriendsViewModel as the
///     single DataContext for HomeView.
/// </summary>
public class HomeDashboardViewModel : ReactiveObject
{
    private readonly FriendsViewModel _friends;
    private readonly NewsViewModel _news;
    private readonly ShellViewModel _shell;
    private readonly IGameUpdaterService _updater;

    public HomeDashboardViewModel(
        ShellViewModel shell,
        NewsViewModel news,
        FriendsViewModel friends,
        IGameLauncherService launcher,
        IGameUpdaterService updater)
    {
        _shell = shell;
        _news = news;
        _friends = friends;
        _updater = updater;

        OpenArticleCommand = ReactiveCommand.Create<NewsArticleViewModel>(article =>
        {
            _news.SelectedArticle = article;
            ArticleOpenRequested?.Invoke(article);
        });

        NavigateToSectionCommand = shell.NavigateCommand;
        OpenContactMenuCommand = ReactiveCommand.Create<FriendItemViewModel>(_ => { });
        OpenFriendsWindowCommand = ReactiveCommand.Create(() => OpenFriendsWindowRequested?.Invoke());

        LaunchGameCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (updater.InstalledVersion is not null)
            {
                await launcher.LaunchAsync();
            }
            else
            {
                shell.NavigateCommand.Execute("Downloads").Subscribe();
            }
        });
    }

    // ── Shell ─────────────────────────────────────────────────────────────
    public string Username => _shell.Username;
    public string Initials => _shell.Initials;
    public string AvatarUrl => _shell.AvatarUrl;
    public UserStatus CurrentStatus => _shell.CurrentStatus;
    public string StatusText => _shell.StatusText;

    // ── News ──────────────────────────────────────────────────────────────
    public ObservableCollection<NewsArticleViewModel> Articles => _news.Articles;
    public bool HasMore => _news.HasMore;
    public bool IsInitialLoading => _news.IsInitialLoading;
    public ICommand LoadNextPageCommand => _news.LoadNextPageCommand;

    // ── Friends ───────────────────────────────────────────────────────────
    public ObservableCollection<FriendItemViewModel> OnlineFriends => _friends.OnlineFriends;
    public bool HasOnlineFriends => _friends.HasOnlineFriends;

    public string LaunchGameLabel => _updater.InstalledVersion is not null ? "Jouer" : "Télécharger";

    public ReactiveCommand<FriendItemViewModel, Unit> OpenContactMenuCommand { get; }
    public ReactiveCommand<NewsArticleViewModel, Unit> OpenArticleCommand { get; }
    public ReactiveCommand<string, Unit> NavigateToSectionCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenFriendsWindowCommand { get; }
    public ReactiveCommand<Unit, Unit> LaunchGameCommand { get; }

    public event Action<NewsArticleViewModel>? ArticleOpenRequested;
    public event Action? OpenFriendsWindowRequested;

    /// <summary>Called by HomeView on Loaded to trigger initial data fetch.</summary>
    public async Task LoadAsync()
    {
        await Task.WhenAll(_news.LoadNewsAsync(), _friends.LoadFriendsAsync());
    }
}