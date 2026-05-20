using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace HellfireStudios.PlateupModManager.Services;

/// <summary>
/// Manages Steam community session cookies and provides subscribe/unsubscribe
/// functionality via steamcommunity.com web endpoints (no API key required).
/// </summary>
public class SteamSessionService
{
    private const string PlateUpAppId = "1599600";
    private const string SteamCommunityBase = "https://steamcommunity.com";

    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;
    private readonly string _sessionFilePath;

    private string? _sessionId;
    private string? _steamLoginSecure;
    private string? _steamId;

    public bool IsLoggedIn => !string.IsNullOrEmpty(_steamLoginSecure) && !string.IsNullOrEmpty(_sessionId);
    public string? SteamId => _steamId;
    public string? DisplayName { get; private set; }

    public SteamSessionService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HellfireStudios",
            "PlateupModManager");
        Directory.CreateDirectory(appDataPath);
        _sessionFilePath = Path.Combine(appDataPath, "steam_session.json");

        _cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = _cookieContainer };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    // ── Cookie Management ────────────────────────────────────────────

    /// <summary>
    /// Sets the session from cookies captured by the WebView2 login flow.
    /// </summary>
    public void SetSessionFromCookies(string sessionId, string steamLoginSecure)
    {
        _sessionId = sessionId;
        _steamLoginSecure = steamLoginSecure;

        // Extract Steam ID from steamLoginSecure cookie (format: "steamId%7C%7CtokenData")
        if (steamLoginSecure.Contains("%7C%7C"))
        {
            _steamId = steamLoginSecure.Split("%7C%7C")[0];
        }
        else if (steamLoginSecure.Contains("||"))
        {
            _steamId = steamLoginSecure.Split("||")[0];
        }

        // Set cookies on the HttpClient's cookie container
        var uri = new Uri(SteamCommunityBase);
        _cookieContainer.Add(uri, new Cookie("sessionid", _sessionId));
        _cookieContainer.Add(uri, new Cookie("steamLoginSecure", _steamLoginSecure));
    }

    /// <summary>
    /// Persists the current session to disk so the user doesn't have to re-login.
    /// </summary>
    public async Task SaveSessionAsync()
    {
        if (!IsLoggedIn) return;

        var session = new SteamSession
        {
            SessionId = _sessionId!,
            SteamLoginSecure = _steamLoginSecure!,
            SteamId = _steamId,
            DisplayName = DisplayName,
            SavedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_sessionFilePath, json);
    }

    /// <summary>
    /// Tries to load a previously saved session from disk.
    /// Returns true if a session was loaded (may still be expired).
    /// </summary>
    public async Task<bool> TryLoadSessionAsync()
    {
        if (!File.Exists(_sessionFilePath))
            return false;

        try
        {
            var json = await File.ReadAllTextAsync(_sessionFilePath);
            var session = JsonSerializer.Deserialize<SteamSession>(json);
            if (session == null || string.IsNullOrEmpty(session.SessionId) || string.IsNullOrEmpty(session.SteamLoginSecure))
                return false;

            SetSessionFromCookies(session.SessionId, session.SteamLoginSecure);
            DisplayName = session.DisplayName;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clears the current session and deletes the saved session file.
    /// </summary>
    public void Logout()
    {
        _sessionId = null;
        _steamLoginSecure = null;
        _steamId = null;
        DisplayName = null;

        if (File.Exists(_sessionFilePath))
            File.Delete(_sessionFilePath);
    }

    /// <summary>
    /// Validates the current session by making a lightweight request to steamcommunity.com.
    /// </summary>
    public async Task<bool> ValidateSessionAsync()
    {
        if (!IsLoggedIn) return false;

        try
        {
            var response = await _httpClient.GetAsync($"{SteamCommunityBase}/my/profile");
            // If we get redirected to login, session is invalid
            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";
            if (finalUrl.Contains("/login"))
                return false;

            // Try to extract display name from profile URL
            if (response.IsSuccessStatusCode)
            {
                var html = await response.Content.ReadAsStringAsync();
                var nameMatch = System.Text.RegularExpressions.Regex.Match(html, @"<span class=""actual_persona_name"">([^<]+)</span>");
                if (nameMatch.Success)
                    DisplayName = nameMatch.Groups[1].Value.Trim();
            }

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Subscribe / Unsubscribe ──────────────────────────────────────

    /// <summary>
    /// Subscribes to a Steam Workshop item.
    /// </summary>
    public async Task<bool> SubscribeAsync(string publishedFileId)
    {
        if (!IsLoggedIn) return false;

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = publishedFileId,
                ["appid"] = PlateUpAppId,
                ["sessionid"] = _sessionId!
            });

            var response = await _httpClient.PostAsync(
                $"{SteamCommunityBase}/sharedfiles/subscribe", content);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Unsubscribes from a Steam Workshop item.
    /// </summary>
    public async Task<bool> UnsubscribeAsync(string publishedFileId)
    {
        if (!IsLoggedIn) return false;

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = publishedFileId,
                ["appid"] = PlateUpAppId,
                ["sessionid"] = _sessionId!
            });

            var response = await _httpClient.PostAsync(
                $"{SteamCommunityBase}/sharedfiles/unsubscribe", content);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Subscribes to multiple workshop items.
    /// Returns the count of successful subscriptions.
    /// </summary>
    public async Task<int> SubscribeManyAsync(IEnumerable<string> publishedFileIds)
    {
        var count = 0;
        foreach (var id in publishedFileIds)
        {
            if (await SubscribeAsync(id))
                count++;
            // Small delay to avoid rate limiting
            await Task.Delay(200);
        }
        return count;
    }

    /// <summary>
    /// Unsubscribes from multiple workshop items.
    /// Returns the count of successful unsubscriptions.
    /// </summary>
    public async Task<int> UnsubscribeManyAsync(IEnumerable<string> publishedFileIds)
    {
        var count = 0;
        foreach (var id in publishedFileIds)
        {
            if (await UnsubscribeAsync(id))
                count++;
            // Small delay to avoid rate limiting
            await Task.Delay(200);
        }
        return count;
    }
}

internal class SteamSession
{
    public string SessionId { get; set; } = string.Empty;
    public string SteamLoginSecure { get; set; } = string.Empty;
    public string? SteamId { get; set; }
    public string? DisplayName { get; set; }
    public DateTime SavedAt { get; set; }
}
