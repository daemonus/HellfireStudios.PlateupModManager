using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using HellfireStudios.PlateupModManager.Services;

namespace HellfireStudios.PlateupModManager.UI.Converters;

/// <summary>
/// Converts a remote image URL to a BitmapImage, using a persistent disk cache.
/// Returns the cached image immediately if available, otherwise downloads in the background.
/// </summary>
public class CachedImageConverter : IValueConverter
{
    private static readonly ImageCacheService Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrEmpty(url))
            return null;

        // If already cached on disk, return immediately
        var cachedPath = Cache.GetCachedPath(url);
        if (cachedPath != null)
            return LoadBitmap(cachedPath);

        // Download in background, return null for now (image will appear empty until cached)
        _ = DownloadAndNotifyAsync(url);
        return null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static BitmapImage LoadBitmap(string localPath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(localPath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.DecodePixelWidth = 128;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static async Task DownloadAndNotifyAsync(string url)
    {
        await Cache.GetOrDownloadAsync(url);
        // Note: The image will appear on next navigation or refresh.
        // For immediate updates we'd need INotifyPropertyChanged on the model,
        // but this is acceptable since images cache permanently after first load.
    }
}
