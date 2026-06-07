using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace Minark.Client.Helpers;

/// <summary>
///     Convertit un <see cref="FlowDocument" /> (contenu d'un RichTextBox)
///     en HTML propre compatible avec <see cref="ContentParser" />.
///     Gère : gras, italique, souligné, barré, listes à puces, listes numérotées,
///     citations (blockquote), paragraphes et sauts de ligne.
/// </summary>
public static class FlowDocumentHtmlConverter
{
    public static string ToHtml(FlowDocument doc)
    {
        if (IsEmpty(doc))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var block in doc.Blocks)
        {
            WriteBlock(block, sb);
        }

        return sb.ToString().Trim();
    }

    public static bool IsEmpty(FlowDocument doc)
    {
        var range = new TextRange(doc.ContentStart, doc.ContentEnd);
        return string.IsNullOrWhiteSpace(range.Text);
    }

    private static void WriteBlock(Block block, StringBuilder sb)
    {
        switch (block)
        {
            case Paragraph p:
                WriteParagraph(p, sb);
                break;
            case List list:
                WriteList(list, sb);
                break;
            case Section section:
                foreach (var child in section.Blocks)
                {
                    WriteBlock(child, sb);
                }

                break;
        }
    }

    private static void WriteParagraph(Paragraph p, StringBuilder sb)
    {
        var isQuote = p.Tag as string == "quote";
        var tag = isQuote ? "blockquote" : "p";

        sb.Append('<').Append(tag).Append('>');
        foreach (var inline in p.Inlines)
        {
            WriteInline(inline, sb);
        }

        sb.Append("</").Append(tag).Append('>');
    }

    private static void WriteList(List list, StringBuilder sb)
    {
        var tag = list.MarkerStyle == TextMarkerStyle.Decimal ? "ol" : "ul";
        sb.Append('<').Append(tag).Append('>');
        foreach (var item in list.ListItems)
        {
            sb.Append("<li>");
            foreach (var child in item.Blocks)
            {
                if (child is Paragraph itemPar)
                {
                    foreach (var inline in itemPar.Inlines)
                    {
                        WriteInline(inline, sb);
                    }
                }
            }

            sb.Append("</li>");
        }

        sb.Append("</").Append(tag).Append('>');
    }

    private static void WriteInline(Inline inline, StringBuilder sb)
    {
        var isBold = inline.FontWeight.ToOpenTypeWeight() >= 600;
        var isItalic = inline.FontStyle == FontStyles.Italic;
        var isUnderline = inline.TextDecorations?.Any(t => t.Location == TextDecorationLocation.Underline) == true;
        var isStrike = inline.TextDecorations?.Any(t => t.Location == TextDecorationLocation.Strikethrough) == true;

        if (isBold)
        {
            sb.Append("<strong>");
        }

        if (isItalic)
        {
            sb.Append("<em>");
        }

        if (isUnderline)
        {
            sb.Append("<u>");
        }

        if (isStrike)
        {
            sb.Append("<s>");
        }

        switch (inline)
        {
            case Run run:
                sb.Append(Escape(run.Text));
                break;
            case LineBreak:
                sb.Append("<br />");
                break;
            case Span span:
                foreach (var child in span.Inlines)
                {
                    WriteInline(child, sb);
                }

                break;
        }

        if (isStrike)
        {
            sb.Append("</s>");
        }

        if (isUnderline)
        {
            sb.Append("</u>");
        }

        if (isItalic)
        {
            sb.Append("</em>");
        }

        if (isBold)
        {
            sb.Append("</strong>");
        }
    }

    private static string Escape(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}