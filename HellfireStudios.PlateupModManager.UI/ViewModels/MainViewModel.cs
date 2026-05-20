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
    private readonly SteamSessionService _steamSessionService;

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

    [ObservableProperty]
    private bool _isSteamLoggedIn;

    public WorkshopBrowserViewModel WorkshopBrowserVm { get; }
    public InstalledModsViewModel InstalledModsVm { get; }
    public ProfilesViewModel ProfilesVm { get; }
    public SettingsViewModel SettingsVm { get; }
    public SteamLoginViewModel SteamLoginVm { get; }
    public AboutViewModel AboutVm { get; }

    public MainViewModel()
    {
        _gameService = new GameService();
        _modManagerService = new ModManagerService();
        _profileService = new ProfileService();
        _workshopService = new SteamWorkshopService();
        _steamSessionService = new SteamSessionService();

        WorkshopBrowserVm = new WorkshopBrowserViewModel(_workshopService, _steamSessionService, this);
        InstalledModsVm = new InstalledModsViewModel(_modManagerService, _steamSessionService, _profileService, this);
        ProfilesVm = new ProfilesViewModel(_profileService, _modManagerService, _steamSessionService, _gameService, this);
        SettingsVm = new SettingsViewModel(_gameService, _profileService, this);
        SteamLoginVm = new SteamLoginViewModel(_steamSessionService, this);
        AboutVm = new AboutViewModel();

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

        // Try to restore a previous Steam session
        await SteamLoginVm.TryRestoreSessionAsync();
        IsSteamLoggedIn = _steamSessionService.IsLoggedIn;

        if (IsConfigured)
        {
            await _profileService.EnsureDefaultProfilesAsync(Settings.WorkshopFolderPath!, _modManagerService);
            await InstalledModsVm.RefreshAsync();
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
    private async Task NavigateToInstalledAsync()
    {
        CurrentView = InstalledModsVm;
        if (IsConfigured) await InstalledModsVm.RefreshAsync();
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

    [RelayCommand]
    private void NavigateToSteamLogin()
    {
        CurrentView = SteamLoginVm;
    }

    [RelayCommand]
    private void NavigateToAbout()
    {
        CurrentView = AboutVm;
    }

    public void OnSteamLoginStateChanged()
    {
        IsSteamLoggedIn = _steamSessionService.IsLoggedIn;
    }

    public void RefreshGameStatus()
    {
        IsGameRunning = _gameService.IsGameRunning();
    }
}
