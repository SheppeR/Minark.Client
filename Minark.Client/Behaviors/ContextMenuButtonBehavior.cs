using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Minark.Client.Behaviors;

/// <summary>
///     Ouvre le ContextMenu d'un Button au clic (placement Bottom par défaut).
///     Remplace les Click handlers "StatusButton_Click", "UserCard_Click", "GearButton_Click"…
///     Usage : beh:ContextMenuButtonBehavior.OpenOnClick="True"
///     beh:ContextMenuButtonBehavior.Placement="Bottom"   (défaut)
/// </summary>
public static class ContextMenuButtonBehavior
{
    public static readonly DependencyProperty OpenOnClickProperty =
        DependencyProperty.RegisterAttached(
            "OpenOnClick",
            typeof(bool),
            typeof(ContextMenuButtonBehavior),
            new PropertyMetadata(false, OnOpenOnClickChanged));

    public static readonly DependencyProperty PlacementProperty =
        DependencyProperty.RegisterAttached(
            "Placement",
            typeof(PlacementMode),
            typeof(ContextMenuButtonBehavior),
            new PropertyMetadata(PlacementMode.Bottom));

    public static bool GetOpenOnClick(DependencyObject d)
    {
        return (bool)d.GetValue(OpenOnClickProperty);
    }

    public static void SetOpenOnClick(DependencyObject d, bool value)
    {
        d.SetValue(OpenOnClickProperty, value);
    }

    public static PlacementMode GetPlacement(DependencyObject d)
    {
        return (PlacementMode)d.GetValue(PlacementProperty);
    }

    public static void SetPlacement(DependencyObject d, PlacementMode value)
    {
        d.SetValue(PlacementProperty, value);
    }

    private static void OnOpenOnClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Button btn)
        {
            return;
        }

        btn.Click -= OnClick;
        if ((bool)e.NewValue)
        {
            btn.Click += OnClick;
        }
    }

    private static void OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } btn)
        {
            return;
        }

        menu.DataContext ??= btn.DataContext;
        menu.PlacementTarget = btn;
        menu.Placement = GetPlacement(btn);
        menu.IsOpen = true;
    }
}