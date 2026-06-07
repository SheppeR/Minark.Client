using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Minark.Client.Converters;

/// <summary>true → Italic, false → Normal. Pour afficher les messages supprimés en italique.</summary>
public class BoolToFontStyleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? FontStyles.Italic : FontStyles.Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}