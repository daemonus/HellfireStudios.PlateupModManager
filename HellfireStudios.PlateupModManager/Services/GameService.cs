using System.Diagnostics;
using Microsoft.Win32;

namespace HellfireStudios.PlateupModManager.Services;

public class GameService
{
    private const string PlateUpAppId = "1599600";
    private const string PlateUpProcessName = "PlateUp";

    /// <summary>
    /// Attempts to auto-detect the Steam installation folder from the Windows registry.
    /// </summary>
    public string? FindSteamInstallPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")
                            ?? Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam");

            return key?.GetValue("InstallPath") as string;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to find the PlateUp! game folder by searching Steam library folders.
    /// </summary>
    public string? FindGameFolder(string? steamPath = null)
    {
        steamPath ??= FindSteamInstallPath();
        if (steamPath == null)
            return null;

        // Check the main Steam library
        var candidates = new List<string> { steamPath };

        // Parse libraryfolders.vdf to find additional library folders
        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFoldersPath))
        {
            try
            {
                var content = File.ReadAllText(libraryFoldersPath);
                // Simple parsing: look for "path" entries
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("\"path\""))
                    {
                        var parts = trimmed.Split('"');
                        if (parts.Length >= 4)
                        {
                            var path = parts[3].Replace("\\\\", "\\");
                            if (Directory.Exists(path))
                                candidates.Add(path);
                        }
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        // Search each library folder for PlateUp!
        foreach (var library in candidates)
        {
            var gamePath = Path.Combine(library, "steamapps", "common", "PlateUp");
            if (Directory.Exists(gamePath))
                return gamePath;
        }

        return null;
    }

    /// <summary>
    /// Finds the workshop content folder for PlateUp! across all Steam libraries.
    /// </summary>
    public string? FindWorkshopFolder(string? steamPath = null)
    {
        steamPath ??= FindSteamInstallPath();
        if (steamPath == null)
            return null;

        var candidates = new List<string> { steamPath };

        var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFoldersPath))
        {
            try
            {
                var content = File.ReadAllText(libraryFoldersPath);
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("\"path\""))
                    {
                        var parts = trimmed.Split('"');
                        if (parts.Length >= 4)
                        {
                            var path = parts[3].Replace("\\\\", "\\");
                            if (Directory.Exists(path))
                                candidates.Add(path);
                        }
                    }
                }
            }
            catch { }
        }

        foreach (var library in candidates)
        {
            var workshopPath = Path.Combine(library, "steamapps", "workshop", "content", PlateUpAppId);
            if (Directory.Exists(workshopPath))
                return workshopPath;
        }

        return null;
    }

    /// <summary>
    /// Returns true if PlateUp! is currently running.
    /// </summary>
    public bool IsGameRunning()
    {
        return Process.GetProcessesByName(PlateUpProcessName).Length > 0;
    }

    /// <summary>
    /// Launches PlateUp! via Steam protocol.
    /// </summary>
    public void LaunchGame()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = $"steam://rungameid/{PlateUpAppId}",
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Launches PlateUp! directly via its executable.
    /// Falls back to Steam protocol if the exe is not found.
    /// </summary>
    public void LaunchGameExe(string? gameFolderPath = null)
    {
        gameFolderPath ??= FindGameFolder();
        if (gameFolderPath != null)
        {
            var exePath = Path.Combine(gameFolderPath, "PlateUp.exe");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = gameFolderPath,
                    UseShellExecute = true
                });
                return;
            }
        }

        // Fallback to Steam protocol
        LaunchGame();
    }

    /// <summary>
    /// Closes PlateUp! if it is running.
    /// </summary>
    public bool CloseGame()
    {
        var processes = Process.GetProcessesByName(PlateUpProcessName);
        if (processes.Length == 0)
            return false;

        foreach (var process in processes)
        {
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5000))
                    process.Kill();
            }
            catch
            {
                // Process may have already exited
            }
        }

        return true;
    }

    /// <summary>
    /// Waits for PlateUp! to start, then waits for it to exit.
    /// Used for the speed run mode workflow.
    /// </summary>
    public async Task WaitForGameToExitAsync(CancellationToken cancellationToken = default)
    {
        // Wait for game to start (up to 30 seconds)
        for (var i = 0; i < 60; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsGameRunning())
                break;
            await Task.Delay(500, cancellationToken);
        }

        // Wait for game to exit
        while (IsGameRunning())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(1000, cancellationToken);
        }
    }

}
