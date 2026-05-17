namespace HellfireStudios.PlateupModManager.Services;

public class ModManagerService
{
    private const string DisabledSuffix = ".disabled";

    /// <summary>
    /// Gets the workshop content folder for PlateUp! mods.
    /// Typically: {SteamLibrary}/steamapps/workshop/content/1599600/
    /// </summary>
    public string? GetWorkshopContentPath(string steamLibraryPath)
    {
        var workshopPath = Path.Combine(steamLibraryPath, "steamapps", "workshop", "content", "1599600");
        return Directory.Exists(workshopPath) ? workshopPath : null;
    }

    /// <summary>
    /// Lists all installed mod folder IDs (both enabled and disabled).
    /// </summary>
    public List<InstalledMod> GetInstalledMods(string workshopContentPath)
    {
        var mods = new List<InstalledMod>();

        if (!Directory.Exists(workshopContentPath))
            return mods;

        foreach (var dir in Directory.GetDirectories(workshopContentPath))
        {
            var folderName = Path.GetFileName(dir);
            var isDisabled = folderName.EndsWith(DisabledSuffix);
            var modId = isDisabled ? folderName[..^DisabledSuffix.Length] : folderName;

            mods.Add(new InstalledMod
            {
                PublishedFileId = modId,
                FolderPath = dir,
                IsEnabled = !isDisabled
            });
        }

        return mods;
    }

    /// <summary>
    /// Enables a mod by removing the .disabled suffix from its folder.
    /// </summary>
    public bool EnableMod(string workshopContentPath, string publishedFileId)
    {
        var disabledPath = Path.Combine(workshopContentPath, publishedFileId + DisabledSuffix);
        var enabledPath = Path.Combine(workshopContentPath, publishedFileId);

        if (Directory.Exists(disabledPath))
        {
            Directory.Move(disabledPath, enabledPath);
            return true;
        }

        return Directory.Exists(enabledPath);
    }

    /// <summary>
    /// Disables a mod by appending .disabled to its folder name.
    /// </summary>
    public bool DisableMod(string workshopContentPath, string publishedFileId)
    {
        var enabledPath = Path.Combine(workshopContentPath, publishedFileId);
        var disabledPath = Path.Combine(workshopContentPath, publishedFileId + DisabledSuffix);

        if (Directory.Exists(enabledPath))
        {
            Directory.Move(enabledPath, disabledPath);
            return true;
        }

        return Directory.Exists(disabledPath);
    }

    /// <summary>
    /// Disables all mods in the workshop content folder.
    /// </summary>
    public int DisableAllMods(string workshopContentPath)
    {
        var count = 0;
        var mods = GetInstalledMods(workshopContentPath);

        foreach (var mod in mods.Where(m => m.IsEnabled))
        {
            if (DisableMod(workshopContentPath, mod.PublishedFileId))
                count++;
        }

        return count;
    }

    /// <summary>
    /// Enables only the mods in the given list, disables all others.
    /// </summary>
    public void ApplyModSet(string workshopContentPath, IEnumerable<string> modIdsToEnable)
    {
        var enableSet = new HashSet<string>(modIdsToEnable);
        var installed = GetInstalledMods(workshopContentPath);

        foreach (var mod in installed)
        {
            if (enableSet.Contains(mod.PublishedFileId))
                EnableMod(workshopContentPath, mod.PublishedFileId);
            else
                DisableMod(workshopContentPath, mod.PublishedFileId);
        }
    }
}

public class InstalledMod
{
    public string PublishedFileId { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Title { get; set; } = string.Empty;
}
