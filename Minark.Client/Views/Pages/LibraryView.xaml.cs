using Minark.Client.ViewModels.Pages;

namespace Minark.Client.Views.Pages;

public partial class LibraryView
{
    public LibraryView(LibraryViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}