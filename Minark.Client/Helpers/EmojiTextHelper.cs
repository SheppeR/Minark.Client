using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using EmojiTextBlock = Emoji.Wpf.TextBlock;

namespace Minark.Client.Helpers;

/// <summary>
///     Segmente texte + emojis. Les emojis sont rendus via <see cref="Emoji.Wpf" />
///     pour un rendu vectoriel couleur identique à ce que fait le chat.
/// </summary>
public static class EmojiTextHelper
{
    private static readonly FontFamily TextFont = new("Segoe UI");

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(EmojiTextHelper),
            new PropertyMetadata(string.Empty, Rebuild));

    public static void SetText(TextBlock tb, string v)
    {
        tb.SetValue(TextProperty, v);
    }

    public static string GetText(TextBlock tb)
    {
        return (string)tb.GetValue(TextProperty);
    }

    private static void Rebuild(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb)
        {
            return;
        }

        // Récupérer la couleur du texte normal depuis le TextBlock lui-même
        var textBrush = tb.Foreground ?? Brushes.White;

        tb.Inlines.Clear();
        var text = e.NewValue as string ?? string.Empty;
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var fontSize = tb.FontSize > 0 ? tb.FontSize : 13;

        foreach (var (segment, isEmoji) in Segment(text))
        {
            if (!isEmoji)
            {
                tb.Inlines.Add(new Run(segment)
                {
                    FontFamily = TextFont,
                    Foreground = textBrush
                });
            }
            else
            {
                // emoji:TextBlock = rendu vectoriel couleur via Emoji.Wpf
                var emojiBlock = new EmojiTextBlock
                {
                    Text = segment,
                    FontSize = fontSize,
                    VerticalAlignment = VerticalAlignment.Center
                };

                tb.Inlines.Add(new InlineUIContainer(emojiBlock)
                {
                    BaselineAlignment = BaselineAlignment.Center
                });
            }
        }
    }

    private static IEnumerable<(string segment, bool isEmoji)> Segment(string text)
    {
        var sb = new StringBuilder();
        bool? cur = null;

        var en = StringInfo.GetTextElementEnumerator(text);
        while (en.MoveNext())
        {
            var el = en.GetTextElement();
            var cp = char.ConvertToUtf32(el, 0);
            var isEmoji = IsEmojiCp(cp);

            if (cur.HasValue && cur.Value != isEmoji)
            {
                yield return (sb.ToString(), cur.Value);
                sb.Clear();
            }

            sb.Append(el);
            cur = isEmoji;
        }

        if (sb.Length > 0 && cur.HasValue)
        {
            yield return (sb.ToString(), cur.Value);
        }
    }

    /// <summary>
    ///     Construit une liste d'<see cref="Inline" /> à partir d'un texte brut en
    ///     remplaçant les segments emoji par des <see cref="InlineUIContainer" />
    ///     portant un <see cref="Emoji.Wpf.TextBlock" /> pour un rendu vectoriel couleur.
    ///     Le <paramref name="template" /> porte le formatage (gras/italique/etc.)
    ///     à appliquer aux segments textuels.
    /// </summary>
    public static IEnumerable<Inline> BuildInlines(string text, Run template)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        var hasEmoji = false;
        foreach (var (_, isEmoji) in Segment(text))
        {
            if (isEmoji)
            {
                hasEmoji = true;
                break;
            }
        }

        // Aucun emoji → on rend un simple Run pour éviter le surcoût InlineUIContainer
        if (!hasEmoji)
        {
            var plain = CloneRun(template, text);
            yield return plain;
            yield break;
        }

        var fontSize = template.FontSize > 0 ? template.FontSize : 13;
        foreach (var (segment, isEmoji) in Segment(text))
        {
            if (!isEmoji)
            {
                yield return CloneRun(template, segment);
            }
            else
            {
                // emoji:TextBlock = drop-in replacement pour TextBlock qui fait
                // un rendu vectoriel couleur via Emoji.Wpf (même système que le chat).
                var emojiBlock = new EmojiTextBlock
                {
                    Text = segment,
                    FontSize = fontSize,
                    VerticalAlignment = VerticalAlignment.Center
                };

                yield return new InlineUIContainer(emojiBlock)
                {
                    BaselineAlignment = BaselineAlignment.Center
                };
            }
        }
    }

    private static Run CloneRun(Run template, string text)
    {
        var r = new Run(text);
        if (template.FontWeight != FontWeights.Normal)
        {
            r.FontWeight = template.FontWeight;
        }

        if (template.FontStyle != FontStyles.Normal)
        {
            r.FontStyle = template.FontStyle;
        }

        if (template.TextDecorations is { Count: > 0 } deco)
        {
            r.TextDecorations = deco;
        }

        if (template.ReadLocalValue(TextElement.FontFamilyProperty) != DependencyProperty.UnsetValue)
        {
            r.FontFamily = template.FontFamily;
        }

        if (template.ReadLocalValue(TextElement.BackgroundProperty) != DependencyProperty.UnsetValue)
        {
            r.Background = template.Background;
        }

        if (template.ReadLocalValue(TextElement.ForegroundProperty) != DependencyProperty.UnsetValue)
        {
            r.Foreground = template.Foreground;
        }

        return r;
    }

    private static bool IsEmojiCp(int cp)
    {
        if (cp == 0x200D || cp == 0xFE0F)
        {
            return true;
        }

        if (cp is >= 0x1F600 and <= 0x1F64F)
        {
            return true;
        }

        if (cp is >= 0x1F300 and <= 0x1F5FF)
        {
            return true;
        }

        if (cp is >= 0x1F680 and <= 0x1F6FF)
        {
            return true;
        }

        if (cp is >= 0x1F700 and <= 0x1F9FF)
        {
            return true;
        }

        if (cp is >= 0x1FA00 and <= 0x1FAFF)
        {
            return true;
        }

        if (cp is >= 0x2600 and <= 0x27BF)
        {
            return true;
        }

        if (cp is >= 0x1F1E0 and <= 0x1F1FF)
        {
            return true;
        }

        return false;
    }
}