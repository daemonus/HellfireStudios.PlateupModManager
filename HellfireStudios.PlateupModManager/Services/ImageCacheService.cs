using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace HellfireStudios.PlateupModManager.Services;

public class ImageCacheService
{
    private readonly string _cacheDirectory;
    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _inFlight = [];
    private readonly object _lock = new();

    public ImageCacheService()
    {
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HellfireStudios",
            "PlateupModManager",
            "ImageCache");

        Directory.CreateDirectory(_cacheDirectory);

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PlateupModManager/1.0");
    }

    /// <summary>
    /// Returns the local cache path for a URL. Null if not yet cached.
    /// </summary>
    public string? GetCachedPath(string imageUrl)
    {
        var path = GetLocalPath(imageUrl);
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Downloads an image to the local cache if not already cached.
    /// Returns the local file path.
    /// </summary>
    public async Task<string?> GetOrDownloadAsync(string imageUrl)
    {
        if (string.IsNullOrEmpty(imageUrl))
            return null;

        var localPath = GetLocalPath(imageUrl);

        if (File.Exists(localPath))
            return localPath;

        // Prevent duplicate concurrent downloads of the same URL
        lock (_lock)
        {
            if (!_inFlight.Add(imageUrl))
                return null; // Already downloading
        }

        try
        {
            var bytes = await _httpClient.GetByteArrayAsync(imageUrl);
            await File.WriteAllBytesAsync(localPath, bytes);
            return localPath;
        }
        catch
        {
            return null;
        }
        finally
        {
            lock (_lock)
            {
                _inFlight.Remove(imageUrl);
            }
        }
    }

    private string GetLocalPath(string imageUrl)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imageUrl)))[..16];
        var extension = Path.GetExtension(new Uri(imageUrl).AbsolutePath);
        if (string.IsNullOrEmpty(extension)) extension = ".jpg";
        return Path.Combine(_cacheDirectory, $"{hash}{extension}");
    }
}
