using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Minark.Client.Behaviors;

/// <summary>
///     Behavior pour le ScrollViewer du chat :
///     - Auto-scroll vers le bas quand Messages.CollectionChanged ajoute un élément
///     - Déclenche LoadMoreCommand quand on scrolle en haut (chargement de l'historique)
///     Usage : beh:ChatScrollBehavior.LoadMoreCommand="{Binding LoadMoreCommand}"
///     beh:ChatScrollBehavior.Messages="{Binding Messages}"
/// </summary>
public static class ChatScrollBehavior
{
    public static readonly DependencyProperty LoadMoreCommandProperty =
        DependencyProperty.RegisterAttached(
            "LoadMoreCommand",
            typeof(ICommand),
            typeof(ChatScrollBehavior),
            new PropertyMetadata(null, OnAttached));

    public static readonly DependencyProperty MessagesProperty =
        DependencyProperty.RegisterAttached(
            "Messages",
            typeof(INotifyCollectionChanged),
            typeof(ChatScrollBehavior),
            new PropertyMetadata(null, OnMessagesChanged));

    private static readonly DependencyProperty SuppressScrollProperty =
        DependencyProperty.RegisterAttached("SuppressScroll", typeof(bool), typeof(ChatScrollBehavior));

    private static readonly DependencyProperty LastLoadProperty =
        DependencyProperty.RegisterAttached("LastLoad", typeof(DateTime), typeof(ChatScrollBehavior),
            new PropertyMetadata(DateTime.MinValue));

    // Stockage du handler pour pouvoir le désabonner
    private static readonly DependencyProperty HandlerProperty =
        DependencyProperty.RegisterAttached("Handler", typeof(NotifyCollectionChangedEventHandler),
            typeof(ChatScrollBehavior));

    public static ICommand? GetLoadMoreCommand(DependencyObject d)
    {
        return (ICommand?)d.GetValue(LoadMoreCommandProperty);
    }

    public static void SetLoadMoreCommand(DependencyObject d, ICommand? value)
    {
        d.SetValue(LoadMoreCommandProperty, value);
    }

    public static INotifyCollectionChanged? GetMessages(DependencyObject d)
    {
        return (INotifyCollectionChanged?)d.GetValue(MessagesProperty);
    }

    public static void SetMessages(DependencyObject d, INotifyCollectionChanged? value)
    {
        d.SetValue(MessagesProperty, value);
    }

    private static void OnAttached(DependencyObject d, DependencyPropertyChangedEventArgs e)
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

    private static void OnMessagesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv)
        {
            return;
        }

        if (e.OldValue is INotifyCollectionChanged oldCol)
        {
            oldCol.CollectionChanged -= GetHandler(sv);
        }

        if (e.NewValue is INotifyCollectionChanged newCol)
        {
            NotifyCollectionChangedEventHandler handler = (_, args) =>
            {
                if (args.Action != NotifyCollectionChangedAction.Add)
                {
                    return;
                }

                sv.Dispatcher.InvokeAsync(() =>
                {
                    sv.SetValue(SuppressScrollProperty, true);
                    sv.ScrollToBottom();
                    sv.SetValue(SuppressScrollProperty, false);
                });
            };
            SetHandler(sv, handler);
            newCol.CollectionChanged += handler;
        }
    }

    private static void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv)
        {
            return;
        }

        if ((bool)sv.GetValue(SuppressScrollProperty))
        {
            return;
        }

        var command = GetLoadMoreCommand(sv);
        if (command is null || !command.CanExecute(null))
        {
            return;
        }

        if (sv.VerticalOffset > 60)
        {
            return;
        }

        var last = (DateTime)sv.GetValue(LastLoadProperty);
        if (DateTime.UtcNow - last < TimeSpan.FromMilliseconds(500))
        {
            return;
        }

        sv.SetValue(LastLoadProperty, DateTime.UtcNow);
        command.Execute(null);
    }

    private static NotifyCollectionChangedEventHandler? GetHandler(DependencyObject d)
    {
        return (NotifyCollectionChangedEventHandler?)d.GetValue(HandlerProperty);
    }

    private static void SetHandler(DependencyObject d, NotifyCollectionChangedEventHandler value)
    {
        d.SetValue(HandlerProperty, value);
    }
}