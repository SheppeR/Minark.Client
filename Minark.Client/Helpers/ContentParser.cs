// ReSharper disable All

using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Minark.Client.Services;

namespace Minark.Client.Helpers;

/// <summary>
///     Parse le HTML produit par Quill.js en blocs WPF.
///     Supporte aussi l'ancien format [img]url[/img] pour compatibilité.
///     Chaque bloc HTML de niveau block (p, h1-h6, li, blockquote, pre)
///     devient un ContentBlock RichText distinct.
/// </summary>
public static class ContentParser
{
    private static readonly Regex LegacyImg =
        new(@"\[img\](.*?)\[/img\]", RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex LegacyInline = new(
        @"(\*\*(.+?)\*\*)|(\*(.+?)\*)|(__(.+?)__)" +
        @"|(~~(.+?)~~)|(`(.+?)`)",
        RegexOptions.Singleline);

    // ─────────────────────────────────────────────────────────────────────────
    // Point d'entrée
    // ─────────────────────────────────────────────────────────────────────────
    public static List<ContentBlock> Parse(string content)
    {
        var blocks = new List<ContentBlock>();
        if (string.IsNullOrWhiteSpace(content)) return blocks;

        bool isHtml = content.TrimStart().StartsWith('<');

        if (isHtml)
            ParseHtml(content, blocks);
        else
            ParseLegacy(content, blocks);

        return blocks;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Parser HTML via HtmlAgilityPack
    // ─────────────────────────────────────────────────────────────────────────
    private static void ParseHtml(string html, List<ContentBlock> blocks)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Quill enveloppe tout dans <p>, <h1-h6>, <ul>/<ol>, <blockquote>, <pre>
        // Si le HTML ne contient que du texte à plat, on le wrappe dans un body fictif
        var root = doc.DocumentNode;

        foreach (var node in root.ChildNodes)
        {
            ProcessNode(node, blocks);
        }
    }

    private static void ProcessNode(HtmlNode node, List<ContentBlock> blocks)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
            if (!string.IsNullOrEmpty(text))
                blocks.Add(MakeTextBlock(new Run(text)));
            return;
        }

        if (node.NodeType != HtmlNodeType.Element) return;

        var tag = node.Name.ToLower();

        switch (tag)
        {
            // ── Balises image ──────────────────────────────────────────────
            case "img":
            {
                var src = node.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(src))
                    blocks.Add(new ContentBlock
                    {
                        Type = ContentBlockType.Image,
                        Value = WebConfig.Resolve(src)
                    });
                break;
            }

            // ── Titres ─────────────────────────────────────────────────────
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
            {
                var size = tag switch
                {
                    "h1" => 22.0,
                    "h2" => 19.0,
                    "h3" => 16.0,
                    _ => 14.0
                };
                var inlines = BuildInlines(node);
                if (inlines.Count > 0)
                {
                    var span = new Span { FontSize = size, FontWeight = FontWeights.Bold };
                    foreach (var il in inlines) span.Inlines.Add(il);
                    blocks.Add(MakeTextBlock(span));
                }

                break;
            }

            // ── Paragraphe ─────────────────────────────────────────────────
            case "p":
            {
                // Un <p> peut contenir une <img> seule
                var imgNode = node.SelectSingleNode(".//img");
                if (imgNode != null && node.InnerText.Trim().Length == 0)
                {
                    var src = imgNode.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src))
                        blocks.Add(new ContentBlock
                        {
                            Type = ContentBlockType.Image,
                            Value = WebConfig.Resolve(src)
                        });
                }
                else
                {
                    var inlines = BuildInlines(node);
                    if (inlines.Count > 0)
                        blocks.Add(new ContentBlock
                        {
                            Type = ContentBlockType.RichText,
                            Inlines = inlines
                        });
                }

                break;
            }

            // ── Listes ─────────────────────────────────────────────────────
            case "ul":
            case "ol":
            {
                int idx = 1;
                foreach (var li in node.ChildNodes.Where(n => n.Name == "li"))
                {
                    var inlines = BuildInlines(li);
                    var bullet = tag == "ol" ? $"{idx++}. " : "• ";
                    inlines.Insert(0, new Run(bullet));
                    blocks.Add(new ContentBlock
                    {
                        Type = ContentBlockType.RichText,
                        Inlines = inlines
                    });
                }

                break;
            }

            // ── Citation ───────────────────────────────────────────────────
            case "blockquote":
            {
                var inlines = BuildInlines(node);
                if (inlines.Count > 0)
                {
                    var span = new Span { Foreground = new SolidColorBrush(Color.FromRgb(153, 153, 176)) };
                    span.Inlines.Add(new Run("❝ "));
                    foreach (var il in inlines) span.Inlines.Add(il);
                    blocks.Add(MakeTextBlock(span));
                }

                break;
            }

            // ── Code block ─────────────────────────────────────────────────
            case "pre":
            {
                var text = HtmlEntity.DeEntitize(node.InnerText);
                var run = new Run(text)
                {
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    Background = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128))
                };
                blocks.Add(MakeTextBlock(run));
                break;
            }

            // ── br standalone ──────────────────────────────────────────────
            case "br":
                // Saut de ligne entre blocs — on ignore, les blocs ont déjà leur espacement
                break;

            // ── Tout autre conteneur ───────────────────────────────────────
            default:
            {
                foreach (var child in node.ChildNodes)
                    ProcessNode(child, blocks);
                break;
            }
        }
    }

    /// <summary>
    ///     Parcourt récursivement les noeuds inline d'un élément HTML
    ///     et retourne une liste de WPF Inlines avec formatage.
    /// </summary>
    private static List<Inline> BuildInlines(HtmlNode parent)
    {
        var inlines = new List<Inline>();
        BuildInlinesRecursive(parent, inlines, bold: false, italic: false, underline: false, strike: false,
            code: false);
        return inlines;
    }

    private static void BuildInlinesRecursive(
        HtmlNode node, List<Inline> inlines,
        bool bold, bool italic, bool underline, bool strike, bool code)
    {
        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                var text = HtmlEntity.DeEntitize(child.InnerText);
                // Remplacer les \n internes par des espaces (Quill n'en met pas dans les inline)
                text = text.Replace("\n", " ");
                if (!string.IsNullOrEmpty(text))
                {
                    // Template Run portant font-family / background / decorations (sans bold/italic)
                    var template = new Run();
                    if (code)
                    {
                        template.FontFamily = new FontFamily("Consolas, Courier New");
                        template.Background = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
                    }

                    if (underline || strike)
                    {
                        var deco = new TextDecorationCollection();
                        if (underline) deco.Add(TextDecorations.Underline[0]);
                        if (strike) deco.Add(TextDecorations.Strikethrough[0]);
                        template.TextDecorations = deco;
                    }

                    // Segmente texte/emojis : emojis en InlineUIContainer (couleur native)
                    foreach (var inline in EmojiTextHelper.BuildInlines(text, template))
                    {
                        // Applique bold / italic en wrappant chaque inline
                        Inline wrapped = inline;
                        if (italic) wrapped = new Italic(wrapped);
                        if (bold) wrapped = new Bold(wrapped);
                        inlines.Add(wrapped);
                    }
                }

                continue;
            }

            if (child.NodeType != HtmlNodeType.Element) continue;

            var tag = child.Name.ToLower();

            switch (tag)
            {
                case "br":
                    inlines.Add(new LineBreak());
                    break;

                case "img":
                {
                    // Image inline — on met un placeholder texte
                    // (sera capturé en bloc image par ProcessNode si seul dans un <p>)
                    break;
                }

                case "strong":
                case "b":
                    BuildInlinesRecursive(child, inlines, true, italic, underline, strike, code);
                    break;

                case "em":
                case "i":
                    BuildInlinesRecursive(child, inlines, bold, true, underline, strike, code);
                    break;

                case "u":
                    BuildInlinesRecursive(child, inlines, bold, italic, true, strike, code);
                    break;

                case "s":
                case "strike":
                case "del":
                    BuildInlinesRecursive(child, inlines, bold, italic, underline, true, code);
                    break;

                case "code":
                    BuildInlinesRecursive(child, inlines, bold, italic, underline, strike, true);
                    break;

                case "a":
                {
                    var href = child.GetAttributeValue("href", "");
                    var sub = new List<Inline>();
                    BuildInlinesRecursive(child, sub, bold, italic, underline, strike, code);
                    var hyperlink = new Hyperlink
                        { NavigateUri = Uri.TryCreate(href, UriKind.Absolute, out var u) ? u : null };
                    foreach (var il in sub) hyperlink.Inlines.Add(il);
                    hyperlink.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212));
                    inlines.Add(hyperlink);
                    break;
                }

                case "span":
                {
                    // Quill utilise des spans avec style= pour la couleur, taille, etc.
                    // On les traverse sans style particulier (le formatage inline suffit)
                    BuildInlinesRecursive(child, inlines, bold, italic, underline, strike, code);
                    break;
                }

                default:
                    // Tout autre tag inline — on traverse
                    BuildInlinesRecursive(child, inlines, bold, italic, underline, strike, code);
                    break;
            }
        }
    }

    private static Inline ApplyFormat(Run run, bool bold, bool italic, bool underline, bool strike, bool code)
    {
        if (code)
        {
            run.FontFamily = new FontFamily("Consolas, Courier New");
            run.Background = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
        }

        var decorations = new TextDecorationCollection();
        if (underline) decorations.Add(TextDecorations.Underline[0]);
        if (strike) decorations.Add(TextDecorations.Strikethrough[0]);
        if (decorations.Count > 0) run.TextDecorations = decorations;

        if (bold && italic) return new Bold(new Italic(run));
        if (bold) return new Bold(run);
        if (italic) return new Italic(run);

        return run;
    }

    private static ContentBlock MakeTextBlock(Inline inline)
        => new() { Type = ContentBlockType.RichText, Inlines = [inline] };

    // ─────────────────────────────────────────────────────────────────────────
    // Parser legacy [img]url[/img] + **gras** *italique*
    // ─────────────────────────────────────────────────────────────────────────
    private static void ParseLegacy(string content, List<ContentBlock> blocks)
    {
        var cursor = 0;
        foreach (Match m in LegacyImg.Matches(content))
        {
            if (m.Index > cursor)
            {
                var text = content[cursor..m.Index];
                if (!string.IsNullOrWhiteSpace(text))
                    blocks.Add(new ContentBlock
                        { Type = ContentBlockType.RichText, Inlines = ParseLegacyInlines(text) });
            }

            var url = m.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(url))
                blocks.Add(new ContentBlock { Type = ContentBlockType.Image, Value = WebConfig.Resolve(url) });
            cursor = m.Index + m.Length;
        }

        if (cursor < content.Length)
        {
            var text = content[cursor..];
            if (!string.IsNullOrWhiteSpace(text))
                blocks.Add(new ContentBlock { Type = ContentBlockType.RichText, Inlines = ParseLegacyInlines(text) });
        }
    }

    private static List<Inline> ParseLegacyInlines(string text)
    {
        var inlines = new List<Inline>();
        var cursor = 0;
        foreach (Match m in LegacyInline.Matches(text))
        {
            if (m.Index > cursor) AppendPlain(inlines, text[cursor..m.Index]);
            if (m.Groups[2].Success) inlines.Add(new Bold(new Run(m.Groups[2].Value)));
            else if (m.Groups[4].Success) inlines.Add(new Italic(new Run(m.Groups[4].Value)));
            else if (m.Groups[6].Success)
                inlines.Add(new Run(m.Groups[6].Value) { TextDecorations = TextDecorations.Underline });
            else if (m.Groups[8].Success)
                inlines.Add(new Run(m.Groups[8].Value) { TextDecorations = TextDecorations.Strikethrough });
            else if (m.Groups[10].Success)
                inlines.Add(new Run(m.Groups[10].Value)
                {
                    FontFamily = new FontFamily("Consolas"),
                    Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128))
                });
            cursor = m.Index + m.Length;
        }

        if (cursor < text.Length) AppendPlain(inlines, text[cursor..]);
        return inlines;
    }

    private static void AppendPlain(List<Inline> inlines, string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrEmpty(lines[i])) inlines.Add(new Run(lines[i]));
            if (i < lines.Length - 1) inlines.Add(new LineBreak());
        }
    }
}