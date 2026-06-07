using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Minark.Client.Services;
using Serilog;

namespace Minark.Client.Converters;

public class StringToImageSourceConverter : IValueConverter
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly Dictionary<string, BitmapImage> Cache = [];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // ── STEP 1 : valeur brute reçue du binding ────────────────────────────
        if (value is not string rawUrl || string.IsNullOrWhiteSpace(rawUrl))
        {
            Log.Debug("[IMG] Convert: valeur nulle ou vide reçue (type={Type})", value?.GetType().Name ?? "null");
            return DependencyProperty.UnsetValue;
        }

        Log.Debug("[IMG] Convert: rawUrl = {RawUrl}", rawUrl);

        // ── STEP 2 : résolution via WebConfig ─────────────────────────────────
        var url = WebConfig.Resolve(rawUrl);
        Log.Debug("[IMG] Resolve: BaseUrl={Base} → url={Url}", WebConfig.BaseUrl, url);

        if (string.IsNullOrWhiteSpace(url))
        {
            Log.Warning("[IMG] Resolve a retourné vide pour rawUrl={RawUrl}", rawUrl);
            return DependencyProperty.UnsetValue;
        }

        // ── STEP 3 : cache ────────────────────────────────────────────────────
        if (Cache.TryGetValue(url, out var cached))
        {
            Log.Debug("[IMG] Cache HIT pour {Url}", url);
            return cached;
        }

        Log.Debug("[IMG] Cache MISS — début chargement pour {Url}", url);

        try
        {
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // ── STEP 4 : téléchargement HTTP ──────────────────────────────
                Log.Debug("[IMG] HTTP GET {Url} ...", url);
                byte[] bytes;
                try
                {
                    bytes = Task.Run(() => Http.GetByteArrayAsync(url)).GetAwaiter().GetResult();
                    Log.Debug("[IMG] HTTP GET OK — {Bytes} octets reçus pour {Url}", bytes.Length, url);
                }
                catch (Exception httpEx)
                {
                    Log.Error(httpEx, "[IMG] HTTP GET ÉCHEC pour {Url}", url);
                    return DependencyProperty.UnsetValue;
                }

                // ── STEP 5 : décodage BitmapImage ─────────────────────────────
                try
                {
                    using var ms = new MemoryStream(bytes);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.CreateOptions = BitmapCreateOptions.None;
                    bitmap.EndInit();
                    if (bitmap.CanFreeze)
                    {
                        bitmap.Freeze();
                    }

                    Cache[url] = bitmap;
                    Log.Debug("[IMG] Bitmap OK — PixelWidth={W} PixelHeight={H} pour {Url}",
                        bitmap.PixelWidth, bitmap.PixelHeight, url);
                    return bitmap;
                }
                catch (Exception bmpEx)
                {
                    Log.Error(bmpEx, "[IMG] Décodage BitmapImage ÉCHEC pour {Url}", url);
                    return DependencyProperty.UnsetValue;
                }
            }

            // ── Fichier local ─────────────────────────────────────────────────
            Log.Debug("[IMG] Chargement local pour {Url}", url);
            var local = new BitmapImage();
            local.BeginInit();
            local.UriSource = new Uri(url, UriKind.Absolute);
            local.CacheOption = BitmapCacheOption.OnLoad;
            local.CreateOptions = BitmapCreateOptions.None;
            local.EndInit();
            if (local.CanFreeze)
            {
                local.Freeze();
            }

            Cache[url] = local;
            Log.Debug("[IMG] Local OK pour {Url}", url);
            return local;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[IMG] Exception non gérée pour {Url}", url);
            return DependencyProperty.UnsetValue;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}