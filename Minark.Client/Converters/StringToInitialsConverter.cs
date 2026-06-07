using System.Globalization;
using System.Windows.Data;
using Minark.Client.Helpers;

namespace Minark.Client.Converters;

/// <summary>String → initiales (1-2 lettres). Délègue à <see cref="StringExtensions.ToInitials" />.</summary>
public class StringToInitialsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as string).ToInitials();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}