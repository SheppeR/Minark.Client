using System.Windows.Controls;
using Minark.Client.ViewModels.Pages;

namespace Minark.Client.Views.Pages;

public partial class NewsDetailView
{
    public NewsDetailView(NewsArticleViewModel article, Frame? hostFrame = null)
    {
        InitializeComponent();
        DataContext = article;
        // NavigationCommands.BrowseBack est bindé directement en XAML sur le bouton Back
        // Le scroll infinite est géré par ScrollLoadBehavior
    }
}