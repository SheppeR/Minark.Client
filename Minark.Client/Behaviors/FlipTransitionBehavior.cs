using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Minark.Client.Views.Pages;

namespace Minark.Client.Behaviors;

/// <summary>
///     Anime le changement de contenu d'un <see cref="ContentControl" /> avec un flip (scale X).
///     Actif uniquement pour les transitions LoginView ↔ RegisterView.
///     Usage : beh:FlipTransitionBehavior.IsEnabled="True"
///     La View doit appeler <see cref="SetContent" /> au lieu d'assigner directement
///     <c>RootContent.Content</c> pour que le behavior intercepte la transition.
/// </summary>
public static class FlipTransitionBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(FlipTransitionBehavior),
            new PropertyMetadata(false));

    private static readonly DependencyProperty IsTransitioningProperty =
        DependencyProperty.RegisterAttached(
            "IsTransitioning", typeof(bool), typeof(FlipTransitionBehavior));

    public static bool GetIsEnabled(DependencyObject d)
    {
        return (bool)d.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(DependencyObject d, bool value)
    {
        d.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    ///     À appeler à la place de <c>cc.Content = view</c>.
    ///     Applique le flip si la transition est Login ↔ Register, sinon swap immédiat.
    /// </summary>
    public static void SetContent(ContentControl cc, object newView)
    {
        if (!GetIsEnabled(cc) || cc.Content is null || (bool)cc.GetValue(IsTransitioningProperty))
        {
            cc.Content = newView;
            return;
        }

        var isAuthTransition =
            (cc.Content is LoginView || cc.Content is RegisterView) &&
            (newView is LoginView || newView is RegisterView);

        if (!isAuthTransition)
        {
            cc.Content = newView;
            return;
        }

        cc.SetValue(IsTransitioningProperty, true);
        cc.RenderTransformOrigin = new Point(0.5, 0.5);
        var scale = new ScaleTransform(1, 1, 0.5, 0.5);
        cc.RenderTransform = scale;

        var shrink = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.22))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        shrink.Completed += (_, _) =>
        {
            cc.Content = newView;
            var grow = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.22))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            grow.Completed += (_, _) =>
            {
                cc.RenderTransform = Transform.Identity;
                cc.SetValue(IsTransitioningProperty, false);
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, shrink);
    }
}