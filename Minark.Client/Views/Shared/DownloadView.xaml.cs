using Minark.Client.ViewModels.Pages;

namespace Minark.Client.Views.Shared;

public partial class DownloadView
{
    public DownloadView(LibraryViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}