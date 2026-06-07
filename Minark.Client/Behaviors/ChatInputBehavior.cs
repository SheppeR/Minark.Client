using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Minark.Client.Behaviors;

/// <summary>
///     Gère les touches clavier dans la zone de saisie du chat :
///     - Enter (sans Ctrl) → SendCommand
///     - Ctrl+Enter → saut de ligne dans un TextBox
///     Usage : beh:ChatInputBehavior.SendCommand="{Binding SendCommand}"
/// </summary>
public static class ChatInputBehavior
{
    public static readonly DependencyProperty SendCommandProperty =
        DependencyProperty.RegisterAttached(
            "SendCommand",
            typeof(ICommand),
            typeof(ChatInputBehavior),
            new PropertyMetadata(null, OnSendCommandChanged));

    public static ICommand? GetSendCommand(DependencyObject d)
    {
        return (ICommand?)d.GetValue(SendCommandProperty);
    }

    public static void SetSendCommand(DependencyObject d, ICommand? value)
    {
        d.SetValue(SendCommandProperty, value);
    }

    private static void OnSendCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        element.PreviewKeyDown -= OnPreviewKeyDown;
        if (e.NewValue is not null)
        {
            element.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not UIElement element)
        {
            return;
        }

        var command = GetSendCommand(element);

        // Enter sans modificateur → envoyer
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None
                                && e.OriginalSource is not TextBox)
        {
            e.Handled = true;
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
            }

            return;
        }

        // Ctrl+Enter → saut de ligne dans le TextBox source
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.Control
                                && e.OriginalSource is TextBox tb)
        {
            e.Handled = true;
            var pos = tb.SelectionStart;
            tb.Text = tb.Text.Insert(pos, Environment.NewLine);
            tb.SelectionStart = pos + Environment.NewLine.Length;
        }
    }
}