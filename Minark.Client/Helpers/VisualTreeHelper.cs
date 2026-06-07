using System.Windows;
using System.Windows.Media;

namespace Minark.Client.Helpers;

public static class VisualTreeExtensions
{
    /// <summary>Remonte l'arbre visuel pour trouver un ancêtre du type T.</summary>
    public static T? TryFindParent<T>(this DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent is not null)
        {
            if (parent is T match)
            {
                return match;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }
}