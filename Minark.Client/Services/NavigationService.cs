using System.Windows.Controls;

namespace Minark.Client.Services;

public interface INavigationService
{
    event Action<UserControl>? OnNavigated;
    void NavigateTo<TView>() where TView : UserControl;
}

public class NavigationService(IServiceProvider sp) : INavigationService
{
    public event Action<UserControl>? OnNavigated;

    public void NavigateTo<TView>() where TView : UserControl
    {
        var view = sp.GetRequiredService<TView>();
        OnNavigated?.Invoke(view);
    }
}