using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using HellfireStudios.PlateupModManager.Models;

namespace HellfireStudios.PlateupModManager.Services;

public class SteamWorkshopService
{
    private const string PlateUpAppId = "1599600";
    private const string QueryFilesUrl = "https://api.steampowered.com/IPublishedFileService/QueryFiles/v1/";

    private readonly HttpClient _httpClient;

    public SteamWorkshopService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<WorkshopQueryResult> QueryWorkshopItemsAsync(
        string apiKey,
        string? searchText = null,
        int page = 1,
        int perPage = 20,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["key"] = apiKey,
            ["appid"] = PlateUpAppId,
            ["query_type"] = "1", // RankedByTrend
            ["numperpage"] = perPage.ToString(),
            ["page"] = page.ToString(),
            ["return_tags"] = "true",
            ["return_previews"] = "true",
            ["return_metadata"] = "true",
            ["strip_description_bbcode"] = "true"
        };

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            queryParams["search_text"] = searchText;
        }

        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        var url = $"{QueryFilesUrl}?{queryString}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<SteamApiResponse>(json);

        return result?.Response ?? new WorkshopQueryResult();
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

public class SteamApiResponse
{
    [JsonPropertyName("response")]
    public WorkshopQueryResult? Response { get; set; }
}

public class WorkshopQueryResult
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("publishedfiledetails")]
    public List<WorkshopMod> Items { get; set; } = [];
}
