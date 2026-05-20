using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using HellfireStudios.PlateupModManager.Models;

namespace HellfireStudios.PlateupModManager.Services;

public class SteamWorkshopService
{
    private const string PlateUpAppId = "1599600";
    private const string CommunityBase = "https://steamcommunity.com";
    private const string DetailsUrl = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";

    private readonly HttpClient _httpClient;

    public SteamWorkshopService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    /// <summary>
    /// Queries workshop items by scraping the Steam Workshop HTML page for item IDs,
    /// then fetching full details via the public GetPublishedFileDetails API.
    /// No API key required.
    /// </summary>
    public async Task<WorkshopQueryResult> QueryWorkshopItemsAsync(
        string? searchText = null,
        int page = 1,
        int perPage = 30,
        CancellationToken cancellationToken = default)
    {
        // Build workshop browse URL
        var browsesort = string.IsNullOrWhiteSpace(searchText) ? "trend" : "textsearch";
        var url = $"{CommunityBase}/workshop/browse/?appid={PlateUpAppId}" +
                  $"&browsesort={browsesort}&section=readytouseitems&p={page}&numperpage={perPage}";

        if (!string.IsNullOrWhiteSpace(searchText))
            url += $"&searchtext={Uri.EscapeDataString(searchText)}";

        // Fetch HTML page
        var html = await _httpClient.GetStringAsync(url, cancellationToken);

        // Extract item IDs from the page
        var ids = ExtractItemIds(html);
        var total = ExtractTotalCount(html) ?? ids.Count;

        if (ids.Count == 0)
            return new WorkshopQueryResult { Total = total };

        // Fetch full item details from the public API
        var items = await FetchItemDetailsAsync(ids, cancellationToken);

        return new WorkshopQueryResult { Total = total, Items = items };
    }

    /// <summary>
    /// Extracts workshop item IDs from the browse page HTML.
    /// </summary>
    private static List<string> ExtractItemIds(string html)
    {
        // Primary: data-publishedfileid attribute on workshop items
        var matches = Regex.Matches(html, @"data-publishedfileid=""(\d+)""");
        var ids = matches.Select(m => m.Groups[1].Value).Distinct().ToList();

        // Fallback: extract from filedetails links
        if (ids.Count == 0)
        {
            matches = Regex.Matches(html, @"sharedfiles/filedetails/\?id=(\d+)");
            ids = matches.Select(m => m.Groups[1].Value).Distinct().ToList();
        }

        return ids;
    }

    /// <summary>
    /// Extracts the total result count from the browse page HTML.
    /// </summary>
    private static int? ExtractTotalCount(string html)
    {
        // Pattern: "Showing 1 - 30 of 1,234 results" in workshopBrowsePagingInfo
        var match = Regex.Match(html, @"of\s+([\d,]+)\s+results", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ""), out var total))
            return total;

        // Alternative: "1,234 results" standalone
        match = Regex.Match(html, @"([\d,]+)\s+results", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value.Replace(",", ""), out total))
            return total;

        return null;
    }

    /// <summary>
    /// Fetches full item details from the public GetPublishedFileDetails API.
    /// </summary>
    private async Task<List<WorkshopMod>> FetchItemDetailsAsync(
        List<string> ids, CancellationToken cancellationToken)
    {
        var items = new List<WorkshopMod>();

        // Batch in groups of 100 (API limit)
        foreach (var batch in ids.Chunk(100))
        {
            var formData = new Dictionary<string, string>
            {
                ["itemcount"] = batch.Length.ToString()
            };
            for (var i = 0; i < batch.Length; i++)
            {
                formData[$"publishedfileids[{i}]"] = batch[i];
            }

            try
            {
                var response = await _httpClient.PostAsync(
                    DetailsUrl,
                    new FormUrlEncodedContent(formData),
                    cancellationToken);

                if (!response.IsSuccessStatusCode) continue;

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);

                var details = doc.RootElement
                    .GetProperty("response")
                    .GetProperty("publishedfiledetails");

                foreach (var item in details.EnumerateArray())
                {
                    // Skip items that failed to resolve (result != 1)
                    if (item.TryGetProperty("result", out var resultProp) && resultProp.GetInt32() != 1)
                        continue;

                    var mod = new WorkshopMod
                    {
                        PublishedFileId = item.GetProperty("publishedfileid").GetString() ?? "",
                        Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                        Description = item.TryGetProperty("file_description", out var d) ? d.GetString() ?? ""
                            : item.TryGetProperty("description", out d) ? d.GetString() ?? "" : "",
                        PreviewUrl = item.TryGetProperty("preview_url", out var p) ? p.GetString() ?? "" : "",
                        Subscriptions = item.TryGetProperty("subscriptions", out var s) ? s.GetInt32() : 0,
                        Favorited = item.TryGetProperty("favorited", out var f) ? f.GetInt32() : 0,
                        TimeUpdated = item.TryGetProperty("time_updated", out var tu) ? tu.GetInt64() : 0
                    };

                    // Parse tags
                    if (item.TryGetProperty("tags", out var tags))
                    {
                        mod.Tags = tags.EnumerateArray()
                            .Select(tag => new WorkshopTag
                            {
                                Tag = tag.TryGetProperty("tag", out var tv) ? tv.GetString() ?? "" : "",
                                DisplayName = tag.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : ""
                            })
                            .ToList();
                    }

                    items.Add(mod);
                }
            }
            catch
            {
                // Continue with next batch on error
            }
        }

        return items;
    }

    /// <summary>
    /// Fetches the list of required dependency IDs for a workshop item.
    /// Returns an empty list if no dependencies or on error.
    /// </summary>
    public async Task<List<string>> GetDependenciesAsync(string publishedFileId)
    {
        var deps = new List<string>();

        try
        {
            var formData = new Dictionary<string, string>
            {
                ["itemcount"] = "1",
                ["publishedfileids[0]"] = publishedFileId
            };

            var response = await _httpClient.PostAsync(
                DetailsUrl, new FormUrlEncodedContent(formData));

            if (!response.IsSuccessStatusCode) return deps;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var item = doc.RootElement
                .GetProperty("response")
                .GetProperty("publishedfiledetails")[0];

            if (item.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    if (child.TryGetProperty("publishedfileid", out var idProp))
                    {
                        var id = idProp.GetString();
                        if (!string.IsNullOrEmpty(id))
                            deps.Add(id);
                    }
                }
            }
        }
        catch
        {
            // Silently return empty list on error
        }

        return deps;
    }

    public void OpenWorkshopPageInSteam(string publishedFileId)
    {
        var url = $"steam://url/CommunityFilePage/{publishedFileId}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    public void OpenWorkshopPageInBrowser(string publishedFileId)
    {
        var url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={publishedFileId}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}

public class WorkshopQueryResult
{
    public int Total { get; set; }
    public List<WorkshopMod> Items { get; set; } = [];
}
