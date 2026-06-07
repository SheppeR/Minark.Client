using System.Globalization;
using System.Windows.Data;
using Minark.Client.Helpers;
using Minark.Shared.Packets;

namespace Minark.Client.Converters;

/// <summary>
///     UserStatus → Color | SolidColorBrush | string selon <c>Mode</c>.
///     Valeurs : "color" (défaut), "brush", "string".
///     Remplace UserStatusToColorConverter, UserStatusToBrushConverter, UserStatusToStringConverter.
/// </summary>
public class UserStatusConverter : IValueConverter
{
    /// <summary>Mode de conversion : "color" (défaut), "brush", "string".</summary>
    public string? Mode { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var status = value is UserStatus s ? s : UserStatus.Offline;
        var mode = Mode ?? parameter as string;
        if (mode?.Equals("string", StringComparison.OrdinalIgnoreCase) == true)
        {
            return status.ToText();
        }

        if (mode?.Equals("brush", StringComparison.OrdinalIgnoreCase) == true)
        {
            return status.ToBrush();
        }

        return status.ToColor();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}