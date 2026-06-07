using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Minark.Client.ViewModels;

namespace Minark.Client.Behaviors;

/// <summary>
///     Gère le clic droit sur un message de chat :
///     remonte le visual tree, trouve le <see cref="ChatMessageViewModel" /> via Tag,
///     et ouvre un ContextMenu dynamique (réaction, modifier, supprimer).
///     Usage :
///     beh:MessageContextMenuBehavior.DeleteCommand="{Binding DeleteMessageCommand, RelativeSource=...}"
///     beh:MessageContextMenuBehavior.EditCommand="{Binding StartEditMessageCommand, RelativeSource=...}"
///     beh:MessageContextMenuBehavior.OpenEmojiCommand="{Binding ...}"
/// </summary>
public static class MessageContextMenuBehavior
{
    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.RegisterAttached("DeleteCommand", typeof(ICommand), typeof(MessageContextMenuBehavior),
            new PropertyMetadata(null, OnAnyCommandChanged));

    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.RegisterAttached("EditCommand", typeof(ICommand), typeof(MessageContextMenuBehavior),
            new PropertyMetadata(null, OnAnyCommandChanged));

    public static readonly DependencyProperty OpenEmojiCommandProperty =
        DependencyProperty.RegisterAttached("OpenEmojiCommand", typeof(ICommand), typeof(MessageContextMenuBehavior),
            new PropertyMetadata(null, OnAnyCommandChanged));

    // Attached flag pour éviter de s'abonner plusieurs fois
    private static readonly DependencyProperty AttachedProperty =
        DependencyProperty.RegisterAttached("Attached", typeof(bool), typeof(MessageContextMenuBehavior));

    public static ICommand? GetDeleteCommand(DependencyObject d)
    {
        return (ICommand?)d.GetValue(DeleteCommandProperty);
    }

    public static void SetDeleteCommand(DependencyObject d, ICommand? v)
    {
        d.SetValue(DeleteCommandProperty, v);
    }

    public static ICommand? GetEditCommand(DependencyObject d)
    {
        return (ICommand?)d.GetValue(EditCommandProperty);
    }

    public static void SetEditCommand(DependencyObject d, ICommand? v)
    {
        d.SetValue(EditCommandProperty, v);
    }

    public static ICommand? GetOpenEmojiCommand(DependencyObject d)
    {
        return (ICommand?)d.GetValue(OpenEmojiCommandProperty);
    }

    public static void SetOpenEmojiCommand(DependencyObject d, ICommand? v)
    {
        d.SetValue(OpenEmojiCommandProperty, v);
    }

    private static void OnAnyCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement el || (bool)d.GetValue(AttachedProperty))
        {
            return;
        }

        el.PreviewMouseRightButtonDown += OnRightClick;
        d.SetValue(AttachedProperty, true);
    }

    private static void OnRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DependencyObject host)
        {
            return;
        }

        // Remonter depuis la source jusqu'à trouver un FrameworkElement avec Tag = ChatMessageViewModel
        var rawSrc = e.OriginalSource as DependencyObject;
        var element = rawSrc is Visual ? rawSrc : (rawSrc as FrameworkContentElement)?.Parent;
        ChatMessageViewModel? msgVm = null;

        while (element is not null)
        {
            if (element is FrameworkElement { Tag: ChatMessageViewModel vm })
            {
                msgVm = vm;
                break;
            }

            var next = element is Visual ? VisualTreeHelper.GetParent(element) : null;
            next ??= (element as FrameworkElement)?.Parent ?? (element as FrameworkContentElement)?.Parent;
            element = next;
        }

        if (msgVm is null || msgVm.IsDeleted)
        {
            return;
        }

        var menu = new ContextMenu();

        var react = new MenuItem { Header = "😊  Emoji" };
        react.Click += (_, _) => GetOpenEmojiCommand(host)?.Execute(msgVm);
        menu.Items.Add(react);

        if (msgVm.IsOwn)
        {
            menu.Items.Add(new Separator());

            var edit = new MenuItem { Header = "✏️  Modifier" };
            edit.Click += (_, _) => GetEditCommand(host)?.Execute(msgVm);
            menu.Items.Add(edit);

            var del = new MenuItem
            {
                Header = "🗑  Supprimer",
                Foreground = new SolidColorBrush(Color.FromRgb(207, 102, 121))
            };
            del.Click += (_, _) => GetDeleteCommand(host)?.Execute(msgVm);
            menu.Items.Add(del);
        }

        menu.PlacementTarget = e.OriginalSource as UIElement;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
        e.Handled = true;
    }
}