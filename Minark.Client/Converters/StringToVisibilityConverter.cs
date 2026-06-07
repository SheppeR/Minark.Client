using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Minark.Client.Converters;

/// <summary>
///     String non vide → Visible.
///     Avec ConverterParameter="inverse" → String non vide → Collapsed (pour afficher le fallback).
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is string s && !string.IsNullOrWhiteSpace(s);
        var inverse = parameter is string p && p.Equals("inverse", StringComparison.OrdinalIgnoreCase);
        return hasValue ^ inverse ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}