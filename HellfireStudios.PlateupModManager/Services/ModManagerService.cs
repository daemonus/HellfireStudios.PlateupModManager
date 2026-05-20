namespace HellfireStudios.PlateupModManager.Services;

public class ModManagerService
{
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
    /// Lists all mod folders currently present in the workshop content directory.
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
                PublishedFileId = folderName,
                FolderPath = dir,
                IsEnabled = true
            });
        }

        return mods;
    }

    // ── Backup ────────────────────────────────────────────────────────

    /// <summary>
    /// Copies mod folders from the workshop into a profile backup directory.
    /// Preserves the full folder structure: backupDir/{modId}/*
    /// </summary>
    public void BackupMods(string workshopContentPath, string backupDir, IEnumerable<string> modIds)
    {
        Directory.CreateDirectory(backupDir);

        foreach (var modId in modIds)
        {
            var sourcePath = Path.Combine(workshopContentPath, modId);
            var destPath = Path.Combine(backupDir, modId);

            if (Directory.Exists(sourcePath))
            {
                CopyDirectoryRecursive(sourcePath, destPath);
            }
        }
    }

    // ── Remove from Workshop ─────────────────────────────────────────

    /// <summary>
    /// Deletes specific mod folders from the workshop content directory.
    /// </summary>
    public int RemoveMods(string workshopContentPath, IEnumerable<string> modIds)
    {
        var count = 0;
        foreach (var modId in modIds)
        {
            var modPath = Path.Combine(workshopContentPath, modId);
            if (Directory.Exists(modPath))
            {
                Directory.Delete(modPath, recursive: true);
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Deletes ALL mod folders from the workshop content directory.
    /// </summary>
    public int RemoveAllMods(string workshopContentPath)
    {
        if (!Directory.Exists(workshopContentPath))
            return 0;

        var dirs = Directory.GetDirectories(workshopContentPath);
        foreach (var dir in dirs)
        {
            Directory.Delete(dir, recursive: true);
        }
        return dirs.Length;
    }

    // ── Restore from Backup ──────────────────────────────────────────

    /// <summary>
    /// Copies mod folders from a profile backup back into the workshop content directory.
    /// </summary>
    public int RestoreMods(string workshopContentPath, string backupDir, IEnumerable<string>? modIds = null)
    {
        if (!Directory.Exists(backupDir))
            return 0;

        var count = 0;
        var dirsToRestore = modIds != null
            ? modIds.Select(id => Path.Combine(backupDir, id)).Where(Directory.Exists)
            : Directory.GetDirectories(backupDir);

        foreach (var sourceDir in dirsToRestore)
        {
            var modId = Path.GetFileName(sourceDir);
            var destPath = Path.Combine(workshopContentPath, modId);

            // Remove existing if present, then copy fresh from backup
            if (Directory.Exists(destPath))
                Directory.Delete(destPath, recursive: true);

            CopyDirectoryRecursive(sourceDir, destPath);
            count++;
        }

        return count;
    }

    // ── Apply Profile ────────────────────────────────────────────────

    /// <summary>
    /// Applies a profile: removes all mods from workshop, then restores only the profile's mods from backup.
    /// </summary>
    public int ApplyProfile(string workshopContentPath, string backupDir)
    {
        RemoveAllMods(workshopContentPath);
        return RestoreMods(workshopContentPath, backupDir);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSubDir);
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
