using System.Globalization;
using System.Windows.Data;
using Minark.Client.Helpers;

namespace Minark.Client.Converters;

/// <summary>
///     Convertit une string (HTML/BBCode) en liste de <see cref="ContentBlock" />
///     via <see cref="ContentParser" />. Utilisé pour afficher le contenu riche
///     des commentaires dans un ItemsControl.
/// </summary>
public class ContentToBlocksConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
        {
            return Array.Empty<ContentBlock>();
        }

        return ContentParser.Parse(s);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}