using System.Net.Http;
using System.Text.Json;

namespace HellfireStudios.PlateupModManager.Services;

public class ModManagerService
{
    private static readonly string TitleCachePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HellfireStudios", "PlateupModManager", "title_cache.json");

    private Dictionary<string, string>? _titleCache;

    private async Task<Dictionary<string, string>> LoadTitleCacheAsync()
    {
        if (_titleCache != null) return _titleCache;

        if (File.Exists(TitleCachePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(TitleCachePath);
                _titleCache = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
            }
            catch
            {
                _titleCache = [];
            }
        }
        else
        {
            _titleCache = [];
        }

        return _titleCache;
    }

    private async Task SaveTitleCacheAsync()
    {
        if (_titleCache == null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(TitleCachePath)!);
        var json = JsonSerializer.Serialize(_titleCache, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(TitleCachePath, json);
    }

    /// <summary>
    /// Lists all mod folders currently present in the workshop content directory.
    /// This is read-only — Steam manages the actual files.
    /// </summary>
    public List<InstalledMod> GetInstalledMods(string workshopContentPath)
    {
        var mods = new List<InstalledMod>();

        if (!Directory.Exists(workshopContentPath))
            return mods;

        foreach (var dir in Directory.GetDirectories(workshopContentPath))
        {
            var folderName = Path.GetFileName(dir);
            mods.Add(new InstalledMod
            {
                PublishedFileId = folderName
            });
        }

        return mods;
    }

    // ── Steam Web API ─────────────────────────────────────────────────

    private static readonly HttpClient HttpClient = new();

    /// <summary>
    /// Fetches mod titles from the Steam Web API using the public GetPublishedFileDetails endpoint.
    /// Returns a dictionary mapping workshop file IDs to their titles.
    /// </summary>
    public async Task<Dictionary<string, string>> ResolveModTitlesAsync(IEnumerable<string> modIds)
    {
        var cache = await LoadTitleCacheAsync();
        var result = new Dictionary<string, string>();
        var idList = modIds.ToList();
        if (idList.Count == 0)
            return result;

        // Return cached titles and collect uncached IDs
        var uncached = new List<string>();
        foreach (var id in idList)
        {
            if (cache.TryGetValue(id, out var cached))
                result[id] = cached;
            else
                uncached.Add(id);
        }

        if (uncached.Count == 0)
            return result;

        // Fetch uncached titles from Steam API
        try
        {
            var formData = new Dictionary<string, string>
            {
                ["itemcount"] = uncached.Count.ToString()
            };
            for (var i = 0; i < uncached.Count; i++)
            {
                formData[$"publishedfileids[{i}]"] = uncached[i];
            }

            var response = await HttpClient.PostAsync(
                "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                new FormUrlEncodedContent(formData));

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                var details = doc.RootElement
                    .GetProperty("response")
                    .GetProperty("publishedfiledetails");

                foreach (var item in details.EnumerateArray())
                {
                    if (item.TryGetProperty("publishedfileid", out var idProp) &&
                        item.TryGetProperty("title", out var titleProp))
                    {
                        var id = idProp.GetString();
                        var title = titleProp.GetString();
                        if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(title))
                        {
                            result[id] = title;
                            cache[id] = title;
                        }
                    }
                }

                await SaveTitleCacheAsync();
            }
        }
        catch
        {
            // Silently fall back — titles will just show IDs
        }

        return result;
    }

}

public class InstalledMod
{
    public string PublishedFileId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
