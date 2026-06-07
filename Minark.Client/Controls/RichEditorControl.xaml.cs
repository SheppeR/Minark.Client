using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Minark.Client.Helpers;

namespace Minark.Client.Controls;

/// <summary>
///     Éditeur de texte riche réutilisable avec toolbar.
///     Produit du HTML compatible avec <see cref="ContentParser" />.
///     Expose <see cref="Html" /> comme DependencyProperty bindable en TwoWay.
/// </summary>
public partial class RichEditorControl
{
    public static readonly DependencyProperty HtmlProperty =
        DependencyProperty.Register(
            nameof(Html),
            typeof(string),
            typeof(RichEditorControl),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnHtmlChanged));

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(RichEditorControl),
            new PropertyMetadata("Écrivez votre commentaire…"));

    public static readonly DependencyProperty ActionContentProperty =
        DependencyProperty.Register(
            nameof(ActionContent),
            typeof(object),
            typeof(RichEditorControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty MaxCharactersProperty =
        DependencyProperty.Register(
            nameof(MaxCharacters),
            typeof(int),
            typeof(RichEditorControl),
            new PropertyMetadata(2000));

    private bool _suppressEditorChange;

    public RichEditorControl()
    {
        InitializeComponent();
        UpdatePlaceholderVisibility();
        UpdateCharCounter();
    }

    public string Html
    {
        get => (string)GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public int MaxCharacters
    {
        get => (int)GetValue(MaxCharactersProperty);
        set => SetValue(MaxCharactersProperty, value);
    }

    public void Clear()
    {
        _suppressEditorChange = true;
        Editor.Document.Blocks.Clear();
        Editor.Document.Blocks.Add(new Paragraph());
        _suppressEditorChange = false;
        Html = string.Empty;
        UpdatePlaceholderVisibility();
        UpdateCharCounter();
    }

    private static void OnHtmlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RichEditorControl ctl)
        {
            return;
        }

        var newHtml = e.NewValue as string ?? string.Empty;
        // Synchronisation externe → éditeur uniquement quand on vide depuis l'extérieur
        if (string.IsNullOrEmpty(newHtml) && !FlowDocumentHtmlConverter.IsEmpty(ctl.Editor.Document))
        {
            ctl._suppressEditorChange = true;
            ctl.Editor.Document.Blocks.Clear();
            ctl.Editor.Document.Blocks.Add(new Paragraph());
            ctl._suppressEditorChange = false;
            ctl.UpdatePlaceholderVisibility();
            ctl.UpdateCharCounter();
        }
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        SyncFromEditor();
    }

    private void SyncFromEditor()
    {
        if (_suppressEditorChange)
        {
            return;
        }

        Html = FlowDocumentHtmlConverter.IsEmpty(Editor.Document)
            ? string.Empty
            : FlowDocumentHtmlConverter.ToHtml(Editor.Document);

        UpdatePlaceholderVisibility();
        UpdateCharCounter();
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        RefreshToolbarState();
    }

    private void UpdatePlaceholderVisibility()
    {
        PlaceholderText.Visibility = FlowDocumentHtmlConverter.IsEmpty(Editor.Document)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void UpdateCharCounter()
    {
        var range = new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd);
        var count = range.Text.Trim().Length;
        CharCounter.Text = MaxCharacters > 0
            ? $"{count} / {MaxCharacters} caractères"
            : $"{count} caractères";

        if (MaxCharacters > 0 && count > MaxCharacters)
        {
            CharCounter.Foreground = Brushes.IndianRed;
        }
        else
        {
            CharCounter.SetResourceReference(TextBlock.ForegroundProperty, "Text3");
        }
    }

    private void RefreshToolbarState()
    {
        var selection = Editor.Selection;

        var weight = selection.GetPropertyValue(TextElement.FontWeightProperty);
        BoldBtn.IsChecked = weight is FontWeight fw && fw.ToOpenTypeWeight() >= 600;

        var style = selection.GetPropertyValue(TextElement.FontStyleProperty);
        ItalicBtn.IsChecked = style is FontStyle fs && fs == FontStyles.Italic;

        var deco = selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        UnderlineBtn.IsChecked = deco?.Any(t => t.Location == TextDecorationLocation.Underline) == true;
        StrikeBtn.IsChecked = deco?.Any(t => t.Location == TextDecorationLocation.Strikethrough) == true;
    }

    // ── Toolbar actions ────────────────────────────────────────────────────
    private void Bold_Click(object sender, RoutedEventArgs e)
    {
        ToggleInlineProperty(TextElement.FontWeightProperty, FontWeights.Bold, FontWeights.Normal,
            v => v is FontWeight fw && fw.ToOpenTypeWeight() >= 600);
        Editor.Focus();
    }

    private void Italic_Click(object sender, RoutedEventArgs e)
    {
        ToggleInlineProperty(TextElement.FontStyleProperty, FontStyles.Italic, FontStyles.Normal,
            v => v is FontStyle fs && fs == FontStyles.Italic);
        Editor.Focus();
    }

    private void Underline_Click(object sender, RoutedEventArgs e)
    {
        ToggleDecoration(TextDecorations.Underline, TextDecorationLocation.Underline);
        Editor.Focus();
    }

    private void Strike_Click(object sender, RoutedEventArgs e)
    {
        ToggleDecoration(TextDecorations.Strikethrough, TextDecorationLocation.Strikethrough);
        Editor.Focus();
    }

    private void Bullet_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleBullets.Execute(null, Editor);
        Editor.Focus();
    }

    private void Numbered_Click(object sender, RoutedEventArgs e)
    {
        EditingCommands.ToggleNumbering.Execute(null, Editor);
        Editor.Focus();
    }

    private void Quote_Click(object sender, RoutedEventArgs e)
    {
        var p = FindParagraphAtCaret();
        if (p is null)
        {
            return;
        }

        if (p.Tag as string == "quote")
        {
            p.Tag = null;
            p.BorderThickness = new Thickness(0);
            p.Padding = new Thickness(0);
            p.ClearValue(Block.BorderBrushProperty);
        }
        else
        {
            p.Tag = "quote";
            p.BorderThickness = new Thickness(3, 0, 0, 0);
            p.Padding = new Thickness(12, 4, 0, 4);
            p.SetResourceReference(Block.BorderBrushProperty, "AccentSoft");
        }

        SyncFromEditor();
        Editor.Focus();
    }

    private void Emoji_Click(object sender, RoutedEventArgs e)
    {
        EmojiPopup.IsOpen = !EmojiPopup.IsOpen;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        var selection = Editor.Selection;
        if (selection.IsEmpty)
        {
            return;
        }

        selection.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal);
        selection.ApplyPropertyValue(TextElement.FontStyleProperty, FontStyles.Normal);
        selection.ApplyPropertyValue(Inline.TextDecorationsProperty, null!);
        RefreshToolbarState();
        Editor.Focus();
    }

    private void EmojiPicker_EmojiSelected(string emoji)
    {
        if (string.IsNullOrEmpty(emoji))
        {
            return;
        }

        Editor.CaretPosition.InsertTextInRun(emoji);
        var newPos = Editor.CaretPosition.GetPositionAtOffset(emoji.Length);
        if (newPos is not null)
        {
            Editor.CaretPosition = newPos;
        }

        EmojiPopup.IsOpen = false;
        Editor.Focus();
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private void ToggleInlineProperty(DependencyProperty prop, object onValue, object offValue,
        Func<object, bool> isOn)
    {
        var selection = Editor.Selection;

        var current = selection.GetPropertyValue(prop);
        var currentlyOn = isOn(current);
        selection.ApplyPropertyValue(prop, currentlyOn ? offValue : onValue);
        RefreshToolbarState();
    }

    private void ToggleDecoration(TextDecorationCollection decoration, TextDecorationLocation location)
    {
        var selection = Editor.Selection;

        var current = selection.GetPropertyValue(Inline.TextDecorationsProperty) as TextDecorationCollection;
        var has = current?.Any(t => t.Location == location) == true;

        if (has)
        {
            var filtered = new TextDecorationCollection(
                current!.Where(t => t.Location != location));
            selection.ApplyPropertyValue(Inline.TextDecorationsProperty, filtered);
        }
        else
        {
            var merged = new TextDecorationCollection();
            if (current is not null)
            {
                foreach (var d in current)
                {
                    merged.Add(d);
                }
            }

            foreach (var d in decoration)
            {
                merged.Add(d);
            }

            selection.ApplyPropertyValue(Inline.TextDecorationsProperty, merged);
        }

        RefreshToolbarState();
    }

    private Paragraph? FindParagraphAtCaret()
    {
        var pointer = Editor.CaretPosition;
        var element = pointer.Parent;
        while (element is not null && element is not Paragraph)
        {
            element = LogicalTreeHelper.GetParent(element);
        }

        return element as Paragraph;
    }
}