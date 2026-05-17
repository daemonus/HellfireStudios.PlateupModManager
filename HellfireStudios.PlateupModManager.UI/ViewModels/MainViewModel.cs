using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellfireStudios.PlateupModManager.Models;
using HellfireStudios.PlateupModManager.Services;

namespace HellfireStudios.PlateupModManager.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly GameService _gameService;
    private readonly ModManagerService _modManagerService;
    private readonly ProfileService _profileService;
    private readonly SteamWorkshopService _workshopService;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isGameRunning;

    [ObservableProperty]
    private bool _isConfigured;

    [ObservableProperty]
    private AppSettings _settings = new();

    public WorkshopBrowserViewModel WorkshopBrowserVm { get; }
    public InstalledModsViewModel InstalledModsVm { get; }
    public ProfilesViewModel ProfilesVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainViewModel()
    {
        _gameService = new GameService();
        _modManagerService = new ModManagerService();
        _profileService = new ProfileService();
        _workshopService = new SteamWorkshopService();

        WorkshopBrowserVm = new WorkshopBrowserViewModel(_workshopService, this);
        InstalledModsVm = new InstalledModsViewModel(_modManagerService, _profileService, this);
        ProfilesVm = new ProfilesViewModel(_profileService, _modManagerService, _gameService, this);
        SettingsVm = new SettingsViewModel(_gameService, _profileService, this);

        CurrentView = InstalledModsVm;
    }

    public async Task InitializeAsync()
    {
        Settings = await _profileService.LoadSettingsAsync();

        if (string.IsNullOrEmpty(Settings.SteamFolderPath))
        {
            Settings.SteamFolderPath = _gameService.FindSteamInstallPath();
        }

        if (string.IsNullOrEmpty(Settings.GameFolderPath))
        {
            Settings.GameFolderPath = _gameService.FindGameFolder(Settings.SteamFolderPath);
        }

        if (string.IsNullOrEmpty(Settings.WorkshopFolderPath))
        {
            Settings.WorkshopFolderPath = _gameService.FindWorkshopFolder(Settings.SteamFolderPath);
        }

        IsConfigured = !string.IsNullOrEmpty(Settings.WorkshopFolderPath)
                       && Directory.Exists(Settings.WorkshopFolderPath);

        if (IsConfigured)
        {
            StatusMessage = $"Workshop folder: {Settings.WorkshopFolderPath}";
            await _profileService.SaveSettingsAsync(Settings);
        }
        else
        {
            StatusMessage = "Workshop folder not found. Please configure in Settings.";
        }

        IsGameRunning = _gameService.IsGameRunning();
        SettingsVm.LoadFrom(Settings);

        if (IsConfigured)
        {
            await _profileService.EnsureDefaultProfilesAsync(Settings.WorkshopFolderPath!, _modManagerService);
            InstalledModsVm.Refresh();
            await ProfilesVm.LoadProfilesAsync();
        }
    }

    [RelayCommand]
    private async Task NavigateToWorkshop()
    {
        CurrentView = WorkshopBrowserVm;
        await WorkshopBrowserVm.EnsureLoadedAsync();
    }

    [RelayCommand]
    private void NavigateToInstalled()
    {
        CurrentView = InstalledModsVm;
        if (IsConfigured) InstalledModsVm.Refresh();
    }

    [RelayCommand]
    private async Task NavigateToProfiles()
    {
        CurrentView = ProfilesVm;
        if (IsConfigured) await ProfilesVm.LoadProfilesAsync();
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = SettingsVm;
    }

    public void RefreshGameStatus()
    {
        IsGameRunning = _gameService.IsGameRunning();
    }
}
