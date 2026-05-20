using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellfireStudios.PlateupModManager.Models;
using HellfireStudios.PlateupModManager.Services;

namespace HellfireStudios.PlateupModManager.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly GameService _gameService;
    private readonly ProfileService _profileService;
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private string _gameFolderPath = string.Empty;

    [ObservableProperty]
    private string _steamFolderPath = string.Empty;

    [ObservableProperty]
    private string _workshopFolderPath = string.Empty;

    public SettingsViewModel(GameService gameService, ProfileService profileService, MainViewModel mainVm)
    {
        _gameService = gameService;
        _profileService = profileService;
        _mainVm = mainVm;
    }

    public void LoadFrom(AppSettings settings)
    {
        GameFolderPath = settings.GameFolderPath ?? string.Empty;
        SteamFolderPath = settings.SteamFolderPath ?? string.Empty;
        WorkshopFolderPath = settings.WorkshopFolderPath ?? string.Empty;
    }

    [RelayCommand]
    private void AutoDetectPaths()
    {
        var steamPath = _gameService.FindSteamInstallPath();
        if (!string.IsNullOrEmpty(steamPath))
        {
            SteamFolderPath = steamPath;
        }

        var gamePath = _gameService.FindGameFolder(steamPath);
        if (!string.IsNullOrEmpty(gamePath))
        {
            GameFolderPath = gamePath;
        }

        var workshopPath = _gameService.FindWorkshopFolder(steamPath);
        if (!string.IsNullOrEmpty(workshopPath))
        {
            WorkshopFolderPath = workshopPath;
        }

        _mainVm.StatusMessage = "Auto-detect complete";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = _mainVm.Settings;
        settings.GameFolderPath = GameFolderPath;
        settings.SteamFolderPath = SteamFolderPath;
        settings.WorkshopFolderPath = WorkshopFolderPath;
        await _profileService.SaveSettingsAsync(settings);

        _mainVm.IsConfigured = !string.IsNullOrEmpty(settings.WorkshopFolderPath)
                                && Directory.Exists(settings.WorkshopFolderPath);

        _mainVm.StatusMessage = "Settings saved";
    }
}
