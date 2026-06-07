using Minark.Client.ViewModels.Pages;

namespace Minark.Client.Views.Pages;

public partial class FriendsView
{
    public FriendsView(FriendsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}