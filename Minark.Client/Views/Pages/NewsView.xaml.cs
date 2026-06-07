using Minark.Client.Helpers;
using Minark.Client.ViewModels.Pages;

namespace Minark.Client.Views.Pages;

public partial class NewsView
{
    public NewsView(NewsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;

        vm.ArticleOpenRequested += article =>
            this.TryFindParent<ShellView>()?.NavigateToArticle(article);
    }
}