using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using Minark.Client.Helpers;
using Minark.Client.Services.Interfaces;
using Minark.Shared.Packets;
using Minark.Shared.Packets.News;
using ReactiveUI;
using Serilog;

// ReSharper disable EventUnsubscriptionViaAnonymousDelegate

namespace Minark.Client.ViewModels.Pages;

/// <summary>
///     Wraps un NewsDto et expose réactions et commentaires.
///     Règle : LikeCount/DislikeCount/CommentCount viennent toujours du serveur.
/// </summary>
public class NewsArticleViewModel : ReactiveObject
{
    private readonly IAuthClientService _auth;
    private readonly ObservableAsPropertyHelper<bool> _canGoNext;
    private readonly ObservableAsPropertyHelper<bool> _canGoPrev;
    private readonly ObservableAsPropertyHelper<bool> _canPostComment;
    private readonly ObservableAsPropertyHelper<string?> _currentCarouselImage;
    private readonly ObservableAsPropertyHelper<bool> _isDisliked;
    private readonly ObservableAsPropertyHelper<bool> _isLiked;
    private readonly INewsClientService _news;
    private int _commentPage;
    private bool _isLoadingComments;

    public NewsArticleViewModel(NewsDto article, IAuthClientService auth, INewsClientService news)
    {
        Article = article;
        ContentBlocks = ContentParser.Parse(article.Content);
        CarouselImages = article.MediaUrls.Select(m => m.Url).ToList();
        HasCarousel = CarouselImages.Count > 0;
        _auth = auth;
        _news = news;

        LikeCount = article.LikeCount;
        DislikeCount = article.DislikeCount;
        CommentCount = article.CommentCount;
        UserReaction = article.UserReaction;

        Observable.FromEventPattern<NotifyCollectionChangedEventArgs>(
                h => Comments.CollectionChanged += (s, e) => h(s, e),
                h => Comments.CollectionChanged -= (s, e) => h(s, e))
            .Select(_ => Unit.Default)
            .StartWith(Unit.Default)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(TopComments));
                this.RaisePropertyChanged(nameof(HasTopComments));
            });

        this.WhenAnyValue(x => x.UserReaction, r => r == ReactionType.Like)
            .ToProperty(this, x => x.IsLiked, out _isLiked);
        this.WhenAnyValue(x => x.UserReaction, r => r == ReactionType.Dislike)
            .ToProperty(this, x => x.IsDisliked, out _isDisliked);

        this.WhenAnyValue(x => x.IsPostingComment, x => x.NewCommentText,
                (posting, text) => !posting && !string.IsNullOrWhiteSpace(text))
            .ToProperty(this, x => x.CanPostComment, out _canPostComment);

        this.WhenAnyValue(x => x.CarouselIndex,
                i => HasCarousel && i < CarouselImages.Count ? CarouselImages[i] : null)
            .ToProperty(this, x => x.CurrentCarouselImage, out _currentCarouselImage);
        this.WhenAnyValue(x => x.CarouselIndex,
                i => HasCarousel && i < CarouselImages.Count - 1)
            .ToProperty(this, x => x.CanGoNext, out _canGoNext);
        this.WhenAnyValue(x => x.CarouselIndex, i => HasCarousel && i > 0)
            .ToProperty(this, x => x.CanGoPrev, out _canGoPrev);

        // Charger les commentaires pour les TopComments
        UiThread.Post(LoadCommentsInternalAsync);

        // Push stats serveur
        news.OnNewsStatsUpdated += OnStatsUpdated;

        // Commandes
        ToggleExpandCommand = ReactiveCommand.Create(() => { IsExpanded = !IsExpanded; });
        LikeCommand = ReactiveCommand.CreateFromTask(LikeAsync);
        DislikeCommand = ReactiveCommand.CreateFromTask(DislikeAsync);
        ToggleCommentsCommand = ReactiveCommand.CreateFromTask(ToggleCommentsAsync);
        LoadCommentsCommand = ReactiveCommand.CreateFromTask(LoadCommentsInternalAsync);
        LoadMoreCommentsCommand = ReactiveCommand.Create(LoadNextCommentPage);
        CarouselNextCommand = ReactiveCommand.Create(CarouselNext);
        CarouselPrevCommand = ReactiveCommand.Create(CarouselPrev);
        PostCommentCommand = ReactiveCommand.CreateFromTask(
            PostCommentAsync,
            this.WhenAnyValue(x => x.IsPostingComment, x => x.NewCommentText,
                (posting, text) => !posting && !string.IsNullOrWhiteSpace(text)));

        // Absorber les exceptions pour ne pas briser le pipeline ReactiveUI
        LikeCommand.ThrownExceptions.Subscribe(ex => Log.Error(ex, "Like error"));
        DislikeCommand.ThrownExceptions.Subscribe(ex => Log.Error(ex, "Dislike error"));
        LoadCommentsCommand.ThrownExceptions.Subscribe(ex => Log.Error(ex, "LoadComments error"));
        PostCommentCommand.ThrownExceptions.Subscribe(ex =>
        {
            IsPostingComment = false;
            Log.Error(ex, "PostComment error");
        });
    }

    public NewsDto Article { get; }
    public List<ContentBlock> ContentBlocks { get; }
    public List<string> CarouselImages { get; }
    public bool HasCarousel { get; }

    public int Id => Article.Id;
    public string Title => Article.Title;
    public string Author => Article.Author;
    public string Category => Article.Category;
    public DateTime PublishedAt => Article.PublishedAt;

    /// <summary>
    ///     URL de l'image principale. Si ImageUrl est null (article sans hero défini),
    ///     on utilise le premier média carousel comme fallback — c'est le cas habituel
    ///     quand les images sont uploadées via le carousel de l'admin.
    /// </summary>
    public string? ImageUrl => !string.IsNullOrWhiteSpace(Article.ImageUrl)
        ? Article.ImageUrl
        : CarouselImages.FirstOrDefault();

    public bool HasHeroImage => !string.IsNullOrWhiteSpace(ImageUrl);

    // ── UI helpers pour le nouveau design ────────────────────────────────
    /// <summary>Index affiché en ghost dans la card (ex: "01", "02").</summary>
    public string DisplayIndex { get; set; } = "01";

    public bool IsExpanded
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Extrait court du contenu en texte brut (120 chars max).</summary>
    public string ExcerptText
    {
        get
        {
            var raw = Article.Content;
            // Retirer les balises HTML (<p>, <br>, <img>, etc.)
            var clean = Regex.Replace(raw, "<[^>]+>", string.Empty);
            // Retirer les balises BBCode [img]...[/img], [b], etc.
            clean = Regex.Replace(clean, @"\[.*?\]", string.Empty);
            // Décoder les entités HTML (&amp; &lt; etc.)
            clean = WebUtility.HtmlDecode(clean).Trim();
            // Normaliser les espaces
            clean = Regex.Replace(clean, @"\s+", " ").Trim();
            return clean.Length <= 150 ? clean : clean[..147] + "...";
        }
    }

    public int CarouselIndex
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(CarouselPosition));
        }
    }

    public int LikeCount
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int DislikeCount
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public int CommentCount
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsCommentsOpen
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool IsPostingComment
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public string NewCommentText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    public ReactionType UserReaction
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public bool HasMoreComments
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    public ObservableCollection<NewsCommentDto> Comments { get; } = [];

    public string? CurrentCarouselImage => _currentCarouselImage.Value;
    public bool CanGoNext => _canGoNext.Value;
    public bool CanGoPrev => _canGoPrev.Value;

    /// <summary>Ex: "2 / 3" — affiché dans le badge du carousel.</summary>
    public string CarouselPosition => HasCarousel
        ? $"{CarouselIndex + 1} / {CarouselImages.Count}"
        : string.Empty;

    public IEnumerable<NewsCommentDto> TopComments => Comments.Take(3);
    public bool HasTopComments => Comments.Count > 0;
    public bool IsLiked => _isLiked.Value;
    public bool IsDisliked => _isDisliked.Value;
    public bool CanPostComment => _canPostComment.Value;

    public ReactiveCommand<Unit, Unit> ToggleExpandCommand { get; }
    public ReactiveCommand<Unit, Unit> LikeCommand { get; }
    public ReactiveCommand<Unit, Unit> DislikeCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleCommentsCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadCommentsCommand { get; }
    public ReactiveCommand<Unit, Unit> LoadMoreCommentsCommand { get; }
    public ReactiveCommand<Unit, Unit> CarouselNextCommand { get; }
    public ReactiveCommand<Unit, Unit> CarouselPrevCommand { get; }
    public ReactiveCommand<Unit, Unit> PostCommentCommand { get; }

    private void OnStatsUpdated(NewsStatsUpdated stats)
    {
        if (stats.NewsId != Id)
        {
            return;
        }

        UiThread.Invoke(() =>
        {
            LikeCount = stats.LikeCount;
            DislikeCount = stats.DislikeCount;
            CommentCount = stats.CommentCount;
        });
    }

    private async Task LikeAsync()
    {
        if (_auth.Token is null)
        {
            return;
        }

        var target = UserReaction == ReactionType.Like ? ReactionType.None : ReactionType.Like;
        var resp = await _news.ReactAsync(_auth.Token, Id, target);
        if (resp.Success)
        {
            UserReaction = resp.UserReaction;
        }
    }

    private async Task DislikeAsync()
    {
        if (_auth.Token is null)
        {
            return;
        }

        var target = UserReaction == ReactionType.Dislike ? ReactionType.None : ReactionType.Dislike;
        var resp = await _news.ReactAsync(_auth.Token, Id, target);
        if (resp.Success)
        {
            UserReaction = resp.UserReaction;
        }
    }

    private async Task ToggleCommentsAsync()
    {
        IsCommentsOpen = !IsCommentsOpen;
        if (IsCommentsOpen && Comments.Count == 0)
        {
            await LoadCommentsInternalAsync();
        }
    }

    private async Task LoadCommentsInternalAsync()
    {
        if (_auth.Token is null || _isLoadingComments)
        {
            return;
        }

        _isLoadingComments = true;
        try
        {
            _commentPage = 0;
            UiThread.Invoke(() => Comments.Clear());
            await LoadNextCommentPageAsync();
        }
        finally
        {
            _isLoadingComments = false;
        }
    }

    private void LoadNextCommentPage()
    {
        // Appelé par la commande ReactiveUI — délègue vers async
        UiThread.Post(async () =>
        {
            if (_isLoadingComments)
            {
                return;
            }

            _isLoadingComments = true;
            try
            {
                await LoadNextCommentPageAsync();
            }
            finally
            {
                _isLoadingComments = false;
            }
        });
    }

    private async Task LoadNextCommentPageAsync()
    {
        if (_auth.Token is null || (!HasMoreComments && _commentPage > 0))
        {
            return;
        }

        var resp = await _news.GetCommentsAsync(_auth.Token, Id, _commentPage + 1);
        if (!resp.Success)
        {
            return;
        }

        UiThread.Invoke(() =>
        {
            foreach (var c in resp.Comments)
            {
                Comments.Add(c);
            }

            _commentPage = resp.Page;
            HasMoreComments = Comments.Count < resp.TotalCount;
            this.RaisePropertyChanged(nameof(HasTopComments));
            this.RaisePropertyChanged(nameof(TopComments));
        });
    }

    private void CarouselNext()
    {
        if (CanGoNext)
        {
            CarouselIndex++;
        }
    }

    private void CarouselPrev()
    {
        if (CanGoPrev)
        {
            CarouselIndex--;
        }
    }

    private async Task PostCommentAsync()
    {
        if (_auth.Token is null || string.IsNullOrWhiteSpace(NewCommentText))
        {
            return;
        }

        IsPostingComment = true;
        try
        {
            var resp = await _news.PostCommentAsync(_auth.Token, Id, NewCommentText.Trim());
            if (resp is { Success: true, Comment: not null })
            {
                Comments.Add(resp.Comment);
                CommentCount++;
                NewCommentText = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NewsArticleViewModel error");
        }
        finally
        {
            IsPostingComment = false;
        }
    }
}