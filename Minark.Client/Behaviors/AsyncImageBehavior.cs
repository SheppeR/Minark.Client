using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Minark.Client.Services;

namespace Minark.Client.Behaviors;

/// <summary>
///     Charge une image HTTP distante de façon asynchrone sans bloquer le thread UI.
///     Usage XAML :
///     beh:AsyncImageBehavior.Source="{Binding ImageUrl}"
///     Remplace ImageBrush.ImageSource="{Binding ImageUrl, Converter={StaticResource UrlToImage}}"
///     pour les images distantes (news cards, carousel).
///     Le converter UrlToImage (StringToImageSourceConverter) bloque le thread UI
///     via Task.Run().GetAwaiter().GetResult() — acceptable pour quelques images,
///     mais problématique pour une liste de cards. Ce behavior charge en async pur.
/// </summary>
public static class AsyncImageBehavior
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly Dictionary<string, BitmapImage> Cache = [];

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.RegisterAttached(
            "Source",
            typeof(string),
            typeof(AsyncImageBehavior),
            new PropertyMetadata(null, OnSourceChanged));

    public static string? GetSource(DependencyObject d)
    {
        return (string?)d.GetValue(SourceProperty);
    }

    public static void SetSource(DependencyObject d, string? value)
    {
        d.SetValue(SourceProperty, value);
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image img)
        {
            return;
        }

        var rawUrl = e.NewValue as string;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            img.Source = null;
            return;
        }

        var url = WebConfig.Resolve(rawUrl);
        if (string.IsNullOrWhiteSpace(url))
        {
            img.Source = null;
            return;
        }

        if (Cache.TryGetValue(url, out var cached))
        {
            img.Source = cached;
            return;
        }

        _ = LoadAsync(img, url);
    }

    private static async Task LoadAsync(Image img, string url)
    {
        try
        {
            BitmapImage bitmap;

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await Http.GetByteArrayAsync(url);
                using var ms = new MemoryStream(bytes);
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.None;
                bitmap.EndInit();
                if (bitmap.CanFreeze)
                {
                    bitmap.Freeze();
                }
            }
            else
            {
                bitmap = new BitmapImage(new Uri(url, UriKind.Absolute));
                if (bitmap.CanFreeze)
                {
                    bitmap.Freeze();
                }
            }

            Cache[url] = bitmap;

            // Repasser sur le thread UI pour affecter la propriété
            await Application.Current.Dispatcher.InvokeAsync(() => img.Source = bitmap);
        }
        catch
        {
            // Image non disponible — on laisse img.Source = null (pas d'image)
        }
    }
}