using System.Windows.Documents;

namespace Minark.Client.Helpers;

public class ContentBlock
{
    public ContentBlockType Type { get; init; }
    public string Value { get; init; } = string.Empty; // URL pour Image
    public List<Inline>? Inlines { get; init; } // pour RichText
}