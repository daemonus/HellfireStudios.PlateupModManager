using System.Text.Json;
using HellfireStudios.PlateupModManager.Models;

namespace HellfireStudios.PlateupModManager.Services;

public class ProfileService
{
    private readonly string _profilesDirectory;
    private readonly string _settingsFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ProfileService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HellfireStudios",
            "PlateupModManager");

        _profilesDirectory = Path.Combine(appDataPath, "Profiles");
        _settingsFilePath = Path.Combine(appDataPath, "settings.json");

        Directory.CreateDirectory(_profilesDirectory);
    }

    // ── Profiles ──────────────────────────────────────────────────────

    public async Task<List<ModProfile>> GetAllProfilesAsync()
    {
        var profiles = new List<ModProfile>();

        if (!Directory.Exists(_profilesDirectory))
            return profiles;

        foreach (var file in Directory.GetFiles(_profilesDirectory, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var profile = JsonSerializer.Deserialize<ModProfile>(json, JsonOptions);
                if (profile != null)
                    profiles.Add(profile);
            }
            catch
            {
                // Skip corrupt profile files
            }
        }

        return profiles.OrderBy(p => p.Name).ToList();
    }

    public async Task<ModProfile?> GetProfileAsync(string profileId)
    {
        var filePath = GetProfileFilePath(profileId);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<ModProfile>(json, JsonOptions);
    }

    public async Task SaveProfileAsync(ModProfile profile)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        await File.WriteAllTextAsync(GetProfileFilePath(profile.Id), json);
    }

    public bool DeleteProfile(string profileId)
    {
        var filePath = GetProfileFilePath(profileId);
        if (!File.Exists(filePath))
            return false;

        File.Delete(filePath);
        return true;
    }

    /// <summary>
    /// Creates a profile from the given mods (stores mod IDs and titles only).
    /// </summary>
    public async Task<ModProfile> CreateProfileFromModsAsync(
        string name,
        string description,
        IEnumerable<InstalledMod> mods)
    {
        var modList = mods.ToList();
        var profile = new ModProfile
        {
            Name = name,
            Description = description,
            Mods = modList.Select(m => new ProfileMod
            {
                PublishedFileId = m.PublishedFileId,
                Title = m.Title
            }).ToList()
        };

        await SaveProfileAsync(profile);
        return profile;
    }

    private const string SpeedRunProfileId = "speed-run-default";
    private const string CleanProfileId = "clean-default";

    /// <summary>
    /// Ensures the built-in default profiles exist.
    /// </summary>
    public async Task EnsureDefaultProfilesAsync(string workshopContentPath, ModManagerService modManager)
    {
        // Speed Run profile — seeded from currently installed mods
        if (await GetProfileAsync(SpeedRunProfileId) == null)
        {
            var installed = modManager.GetInstalledMods(workshopContentPath);
            var speedRun = new ModProfile
            {
                Id = SpeedRunProfileId,
                Name = "Speed Run Leaderboard",
                Description = "Temporarily enables mods to view the speed run leaderboard, then removes them so you can submit times.",
                IsSpeedRunProfile = true,
                Mods = installed.Select(m => new ProfileMod
                {
                    PublishedFileId = m.PublishedFileId,
                    Title = string.IsNullOrEmpty(m.Title) ? m.PublishedFileId : m.Title
                }).ToList()
            };
            await SaveProfileAsync(speedRun);
        }

        // Clean profile — empty mod list, applying it unsubscribes from everything
        if (await GetProfileAsync(CleanProfileId) == null)
        {
            var clean = new ModProfile
            {
                Id = CleanProfileId,
                Name = "Clean (No Mods)",
                Description = "Unsubscribes from all mods for a vanilla PlateUp! experience.",
                Mods = []
            };
            await SaveProfileAsync(clean);
        }
    }

    private string GetProfileFilePath(string profileId)
    {
        return Path.Combine(_profilesDirectory, $"{profileId}.json");
    }

    // ── Settings ──────────────────────────────────────────────────────

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(_settingsFilePath))
            return new AppSettings();

        try
        {
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json);
    }
}
