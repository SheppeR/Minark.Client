using System.Windows;

namespace Minark.Client.Behaviors;

/// <summary>
///     Donne le focus à un élément dès qu'il devient visible.
///     Remplace les "SearchBox.Focus()" dans le code-behind.
///     Usage : beh:FocusOnVisibilityBehavior.FocusOnVisible="True"
/// </summary>
public static class FocusOnVisibilityBehavior
{
    public static readonly DependencyProperty FocusOnVisibleProperty =
        DependencyProperty.RegisterAttached(
            "FocusOnVisible",
            typeof(bool),
            typeof(FocusOnVisibilityBehavior),
            new PropertyMetadata(false, OnFocusOnVisibleChanged));

    public static bool GetFocusOnVisible(DependencyObject d)
    {
        return (bool)d.GetValue(FocusOnVisibleProperty);
    }

    public static void SetFocusOnVisible(DependencyObject d, bool value)
    {
        d.SetValue(FocusOnVisibleProperty, value);
    }

    private static void OnFocusOnVisibleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        element.IsVisibleChanged -= OnIsVisibleChanged;
        if ((bool)e.NewValue)
        {
            element.IsVisibleChanged += OnIsVisibleChanged;
        }
    }

    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is FrameworkElement { IsVisible: true } el)
        {
            el.Dispatcher.InvokeAsync(() => el.Focus());
        }
    }
}