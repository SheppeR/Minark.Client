using System.Collections.ObjectModel;
using System.Reactive;
using Minark.Client.Helpers;
using Minark.Client.Services.Interfaces;
using ReactiveUI;
using Serilog;

namespace Minark.Client.ViewModels.Pages;

public class NewsViewModel : ViewModelBase
{
    private readonly IAuthClientService _auth;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly INewsClientService _news;

    public NewsViewModel(INewsClientService news, IAuthClientService auth)
    {
        _news = news;
        _auth = auth;

        this.WhenAnyValue(x => x.IsLoading, x => x.Articles)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(IsInitialLoading));
                this.RaisePropertyChanged(nameof(IsLoadingMore));
            });

        news.OnNewsChanged += _ =>
            UiThread.Post(async () => await LoadNewsAsync());

        LoadNewsCommand = ReactiveCommand.CreateFromTask(LoadNewsAsync);
        LoadNextPageCommand = ReactiveCommand.CreateFromTask(LoadNextPageAsync);
        OpenArticleCommand = ReactiveCommand.Create<NewsArticleViewModel>(article =>
        {
            SelectedArticle = article;
            ArticleOpenRequested?.Invoke(article);
        });

        LoadNewsCommand.ThrownExceptions
            .Subscribe(ex =>
            {
                IsLoading = false;
                _loadLock.Release();
                Log.Error(ex, "NewsViewModel.LoadNews error");
            });
        LoadNextPageCommand.ThrownExceptions
            .Subscribe(ex =>
            {
                IsLoading = false;
                _loadLock.Release();
                Log.Error(ex, "NewsViewModel.LoadNextPage error");
            });
    }

    public NewsArticleViewModel? SelectedArticle { get; set; }

    public ObservableCollection<NewsArticleViewModel> Articles
    {
        get;
        init => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    public int CurrentPage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasMore
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = true;

    public bool IsLoading
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int TotalCount
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsInitialLoading => IsLoading && Articles.Count == 0;
    public bool IsLoadingMore => IsLoading && Articles.Count > 0;

    public ReactiveCommand<Unit, Unit> LoadNewsCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadNextPageCommand { get; }
    public ReactiveCommand<NewsArticleViewModel, Unit> OpenArticleCommand { get; }

    public event Action<NewsArticleViewModel>? ArticleOpenRequested;

    public async Task LoadNewsAsync()
    {
        if (_auth.Token is null || IsLoading)
        {
            return;
        }

        Articles.Clear();
        CurrentPage = 0;
        HasMore = true;
        await LoadNextPageAsync();
    }

    public async Task LoadNextPageAsync()
    {
        if (_auth.Token is null || !HasMore)
        {
            return;
        }

        if (!await _loadLock.WaitAsync(0))
        {
            return;
        }

        IsLoading = true;
        try
        {
            var nextPage = CurrentPage + 1;
            var response = await _news.GetNewsAsync(_auth.Token, nextPage);

            Log.Information("[NEWS-VM] Réponse reçue: {Count} articles, TotalCount={Total}",
                response.News.Count, response.TotalCount);

            foreach (var a in response.News)
            {
                Log.Debug("[NEWS-VM] Article id={Id} ImageUrl={ImageUrl} MediaCount={MediaCount}",
                    a.Id, a.ImageUrl ?? "null", a.MediaUrls.Count);

                var vm = new NewsArticleViewModel(a, _auth, _news)
                {
                    DisplayIndex = (Articles.Count + 1).ToString("D2")
                };
                Articles.Add(vm);
            }

            TotalCount = response.TotalCount;
            CurrentPage = nextPage;
            HasMore = Articles.Count < TotalCount;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NewsViewModel error");
        }
        finally
        {
            IsLoading = false;
            _loadLock.Release();
        }
    }
}