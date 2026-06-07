using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Minark.Client.Behaviors;

/// <summary>
///     Déclenche une commande quand le ScrollViewer atteint le bas (infinite scroll)
///     ou le haut (load more older).
///     Usage : beh:ScrollLoadBehavior.LoadMoreCommand="{Binding LoadMoreCommand}"
///     beh:ScrollLoadBehavior.LoadAtTop="True"          (défaut: False = bas)
///     beh:ScrollLoadBehavior.Threshold="120"           (px avant la limite)
/// </summary>
public static class ScrollLoadBehavior
{
    public static readonly DependencyProperty LoadMoreCommandProperty =
        DependencyProperty.RegisterAttached(
            "LoadMoreCommand",
            typeof(ICommand),
            typeof(ScrollLoadBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty LoadAtTopProperty =
        DependencyProperty.RegisterAttached(
            "LoadAtTop",
            typeof(bool),
            typeof(ScrollLoadBehavior),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ThresholdProperty =
        DependencyProperty.RegisterAttached(
            "Threshold",
            typeof(double),
            typeof(ScrollLoadBehavior),
            new PropertyMetadata(120.0));

    public static readonly DependencyProperty ThrottleMillisProperty =
        DependencyProperty.RegisterAttached(
            "ThrottleMillis",
            typeof(int),
            typeof(ScrollLoadBehavior),
            new PropertyMetadata(500));

    // Stocke la dernière exécution pour le throttle
    private static readonly DependencyProperty LastFireProperty =
        DependencyProperty.RegisterAttached("LastFire", typeof(DateTime), typeof(ScrollLoadBehavior),
            new PropertyMetadata(DateTime.MinValue));

    public static ICommand? GetLoadMoreCommand(DependencyObject d)
    {
        return (ICommand?)d.GetValue(LoadMoreCommandProperty);
    }

    public static void SetLoadMoreCommand(DependencyObject d, ICommand? value)
    {
        d.SetValue(LoadMoreCommandProperty, value);
    }

    public static bool GetLoadAtTop(DependencyObject d)
    {
        return (bool)d.GetValue(LoadAtTopProperty);
    }

    public static void SetLoadAtTop(DependencyObject d, bool value)
    {
        d.SetValue(LoadAtTopProperty, value);
    }

    public static double GetThreshold(DependencyObject d)
    {
        return (double)d.GetValue(ThresholdProperty);
    }

    public static void SetThreshold(DependencyObject d, double value)
    {
        d.SetValue(ThresholdProperty, value);
    }

    public static int GetThrottleMillis(DependencyObject d)
    {
        return (int)d.GetValue(ThrottleMillisProperty);
    }

    public static void SetThrottleMillis(DependencyObject d, int value)
    {
        d.SetValue(ThrottleMillisProperty, value);
    }

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv)
        {
            return;
        }

        sv.ScrollChanged -= OnScrollChanged;
        if (e.NewValue is not null)
        {
            sv.ScrollChanged += OnScrollChanged;
        }
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv)
        {
            return;
        }

        var command = GetLoadMoreCommand(sv);
        if (command is null || !command.CanExecute(null))
        {
            return;
        }

        var threshold = GetThreshold(sv);
        var atTop = GetLoadAtTop(sv);
        var reached = atTop
            ? sv.VerticalOffset < threshold
            : sv.VerticalOffset >= sv.ScrollableHeight - threshold;

        if (!reached)
        {
            return;
        }

        var throttle = TimeSpan.FromMilliseconds(GetThrottleMillis(sv));
        var last = (DateTime)sv.GetValue(LastFireProperty);
        if (DateTime.UtcNow - last < throttle)
        {
            return;
        }

        sv.SetValue(LastFireProperty, DateTime.UtcNow);
        command.Execute(null);
    }
}