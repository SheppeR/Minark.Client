using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Minark.Client.Converters;

/// <summary>
///     bool/int/null → Visibility.
///     Mettre <c>Inverse="True"</c> à l'instanciation pour inverser (false/null → Visible).
///     ConverterParameter="inverse" est également accepté au binding pour rester compatible.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    /// <summary>Quand True, inverse la logique : false/null → Visible.</summary>
    public bool Inverse { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value switch
        {
            null => false,
            bool b => b,
            int i => i > 0,
            _ => true
        };
        var inverse = Inverse || (parameter is string p && p.Equals("inverse", StringComparison.OrdinalIgnoreCase));
        return visible ^ inverse ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var inverse = Inverse || (parameter is string p && p.Equals("inverse", StringComparison.OrdinalIgnoreCase));
        var isVisible = value is Visibility.Visible;
        return inverse ? !isVisible : isVisible;
    }
}