using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Minark.Client.Helpers;

/// <summary>
///     AttachedProperty pour binder une List&lt;Inline&gt; sur un TextBlock.
///     Usage XAML : h:InlinesBinder.Inlines="{Binding Inlines}"
/// </summary>
public static class InlinesBinder
{
    public static readonly DependencyProperty InlinesProperty =
        DependencyProperty.RegisterAttached(
            "Inlines",
            typeof(IEnumerable<Inline>),
            typeof(InlinesBinder),
            new PropertyMetadata(null, OnInlinesChanged));

    public static void SetInlines(TextBlock element, IEnumerable<Inline> value)
    {
        element.SetValue(InlinesProperty, value);
    }

    public static IEnumerable<Inline> GetInlines(TextBlock element)
    {
        return (IEnumerable<Inline>)element.GetValue(InlinesProperty);
    }

    private static void OnInlinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb)
        {
            return;
        }

        tb.Inlines.Clear();
        if (e.NewValue is IEnumerable<Inline> inlines)
        {
            foreach (var inline in inlines)
            {
                tb.Inlines.Add(inline);
            }
        }
    }
}