using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellfireStudios.PlateupModManager.Models;
using HellfireStudios.PlateupModManager.Services;

namespace HellfireStudios.PlateupModManager.UI.ViewModels;

public partial class ProfilesViewModel : ObservableObject
{
    private readonly ProfileService _profileService;
    private readonly ModManagerService _modManagerService;
    private readonly GameService _gameService;
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private bool _isSpeedRunModeRunning;

    [ObservableProperty]
    private string _speedRunStatus = string.Empty;

    private CancellationTokenSource? _speedRunCts;

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];

    public ProfilesViewModel(
        ProfileService profileService,
        ModManagerService modManagerService,
        GameService gameService,
        MainViewModel mainVm)
    {
        _profileService = profileService;
        _modManagerService = modManagerService;
        _gameService = gameService;
        _mainVm = mainVm;
    }

    public async Task LoadProfilesAsync()
    {
        var profiles = await _profileService.GetAllProfilesAsync();
        Profiles.Clear();

        foreach (var profile in profiles)
        {
            Profiles.Add(new ProfileItemViewModel(profile));
        }
    }

    [RelayCommand]
    private void ApplyProfile(ProfileItemViewModel? profileVm)
    {
        if (profileVm == null) return;

        var workshopPath = _mainVm.Settings.WorkshopFolderPath;
        if (string.IsNullOrEmpty(workshopPath)) return;

        var backupDir = _profileService.GetProfileBackupPath(profileVm.Profile.Id);
        var count = _modManagerService.ApplyProfile(workshopPath, backupDir);
        _mainVm.StatusMessage = $"Applied profile '{profileVm.Name}' ({count} mods restored)";
        _mainVm.InstalledModsVm.Refresh();
    }

    [RelayCommand]
    private async Task DeleteProfileAsync(ProfileItemViewModel? profileVm)
    {
        if (profileVm == null) return;

        _profileService.DeleteProfile(profileVm.Profile.Id);
        await LoadProfilesAsync();
        _mainVm.StatusMessage = $"Deleted profile '{profileVm.Name}'";
    }

    [RelayCommand]
    private async Task SpeedRunModeAsync(ProfileItemViewModel? profileVm)
    {
        if (profileVm == null) return;

        var workshopPath = _mainVm.Settings.WorkshopFolderPath;
        if (string.IsNullOrEmpty(workshopPath)) return;

        // If game is running, ask user to confirm closing it first
        if (_gameService.IsGameRunning())
        {
            var result = MessageBox.Show(
                "PlateUp! is currently running and needs to be closed before Speed Run Mode can start.\n\nClose PlateUp! now?",
                "Close PlateUp!?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        IsSpeedRunModeRunning = true;
        _speedRunCts = new CancellationTokenSource();

        var backupDir = _profileService.GetProfileBackupPath(profileVm.Profile.Id);
        var progress = new Progress<string>(msg =>
        {
            SpeedRunStatus = msg;
            _mainVm.StatusMessage = $"[Speed Run] {msg}";
        });

        try
        {
            await _gameService.RunSpeedRunModeAsync(
                workshopPath,
                backupDir,
                _modManagerService,
                progress,
                _speedRunCts.Token);
        }
        catch (OperationCanceledException)
        {
            SpeedRunStatus = "Speed run mode cancelled";
            _modManagerService.RemoveAllMods(workshopPath);
        }
        catch (Exception ex)
        {
            SpeedRunStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsSpeedRunModeRunning = false;
            _speedRunCts?.Dispose();
            _speedRunCts = null;
            _mainVm.RefreshGameStatus();
        }
    }

    [RelayCommand]
    private void CancelSpeedRunMode()
    {
        _speedRunCts?.Cancel();
    }
}

public partial class ProfileItemViewModel : ObservableObject
{
    public ModProfile Profile { get; }

    public string Name => Profile.Name;
    public string Description => Profile.Description;
    public bool IsSpeedRunProfile => Profile.IsSpeedRunProfile;
    public int ModCount => Profile.Mods.Count;
    public string ModSummary => $"{ModCount} mod{(ModCount == 1 ? "" : "s")}";
    public string UpdatedAt => Profile.UpdatedAt.ToLocalTime().ToString("g");

    public ProfileItemViewModel(ModProfile profile)
    {
        Profile = profile;
    }
}
