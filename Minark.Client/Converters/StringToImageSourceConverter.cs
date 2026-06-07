using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Minark.Client.Services;

namespace Minark.Client.Converters;

/// <summary>
///     String URL → BitmapImage pour WPF Image.Source et PersonPicture.ProfilePicture.
///     CORRECTIF : retourne DependencyProperty.UnsetValue (et non null) quand l'URL
///     est vide ou invalide. Retourner null amène WPF à tenter une conversion
///     null→ImageSource via ImageSourceConverter qui lève NotSupportedException.
///     UnsetValue dit à WPF "ne pas appliquer de valeur" → pas d'erreur, pas d'image.
///     Chargement avec BitmapCreateOptions.None + BitmapCacheOption.OnLoad :
///     charge l'image complètement à EndInit() — fiable pour HTTP local.
///     DelayCreation causait des images noires car WPF reportait le décodage
///     après Freeze() et ne relançait jamais le chargement.
/// </summary>
public class StringToImageSourceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string rawUrl || string.IsNullOrWhiteSpace(rawUrl))
        {
            return DependencyProperty.UnsetValue;
        }

        var url = WebConfig.Resolve(rawUrl);
        if (string.IsNullOrWhiteSpace(url))
        {
            return DependencyProperty.UnsetValue;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(url, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.None;
            bitmap.EndInit();
            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            return bitmap;
        }
        catch
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}