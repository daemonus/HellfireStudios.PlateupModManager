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
    private readonly SteamSessionService _steamSessionService;
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
        SteamSessionService steamSessionService,
        GameService gameService,
        MainViewModel mainVm)
    {
        _profileService = profileService;
        _modManagerService = modManagerService;
        _steamSessionService = steamSessionService;
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

        // Resolve missing titles from Steam API
        var modsNeedingTitles = profiles
            .SelectMany(p => p.Mods)
            .Where(m => string.IsNullOrEmpty(m.Title) || m.Title == m.PublishedFileId)
            .ToList();

        if (modsNeedingTitles.Count > 0)
        {
            var ids = modsNeedingTitles.Select(m => m.PublishedFileId).Distinct();
            var titles = await _modManagerService.ResolveModTitlesAsync(ids);

            var dirty = new HashSet<string>();
            foreach (var mod in modsNeedingTitles)
            {
                if (titles.TryGetValue(mod.PublishedFileId, out var title))
                {
                    mod.Title = title;
                    dirty.Add(mod.PublishedFileId);
                }
            }

            // Persist updated titles so we don't need to re-fetch next time
            if (dirty.Count > 0)
            {
                foreach (var profile in profiles)
                {
                    if (profile.Mods.Any(m => dirty.Contains(m.PublishedFileId)))
                        await _profileService.SaveProfileAsync(profile);
                }

                // Refresh the UI with updated titles
                Profiles.Clear();
                foreach (var profile in profiles)
                {
                    Profiles.Add(new ProfileItemViewModel(profile));
                }
            }
        }
    }

    [RelayCommand]
    private async Task ApplyProfileAsync(ProfileItemViewModel? profileVm)
    {
        if (profileVm == null) return;

        if (!_steamSessionService.IsLoggedIn)
        {
            _mainVm.StatusMessage = "Sign in to your Steam account first (🔑 Steam Account)";
            return;
        }

        var workshopPath = _mainVm.Settings.WorkshopFolderPath;
        if (string.IsNullOrEmpty(workshopPath)) return;

        var profileModIds = profileVm.Profile.Mods.Select(m => m.PublishedFileId).ToHashSet();
        var currentModIds = _modManagerService.GetInstalledMods(workshopPath)
            .Select(m => m.PublishedFileId).ToHashSet();

        var toUnsub = currentModIds.Except(profileModIds).ToList();
        var toSub = profileModIds.Except(currentModIds).ToList();

        _mainVm.StatusMessage = $"Applying profile '{profileVm.Name}'...";

        if (toUnsub.Count > 0)
        {
            _mainVm.StatusMessage = $"Unsubscribing from {toUnsub.Count} mods...";
            await _steamSessionService.UnsubscribeManyAsync(toUnsub);
        }

        if (toSub.Count > 0)
        {
            _mainVm.StatusMessage = $"Subscribing to {toSub.Count} mods...";
            await _steamSessionService.SubscribeManyAsync(toSub);
        }

        _mainVm.StatusMessage = $"Applied profile '{profileVm.Name}' (−{toUnsub.Count} / +{toSub.Count})";
        await _mainVm.InstalledModsVm.RefreshAsync();
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

        if (!_steamSessionService.IsLoggedIn)
        {
            _mainVm.StatusMessage = "Sign in to your Steam account first (🔑 Steam Account)";
            return;
        }

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
        var profileModIds = profileVm.Profile.Mods.Select(m => m.PublishedFileId).ToList();

        try
        {
            // Step 1: Close game if running
            if (_gameService.IsGameRunning())
            {
                SpeedRunStatus = "Closing PlateUp!...";
                _mainVm.StatusMessage = "[Speed Run] Closing PlateUp!...";
                _gameService.CloseGame();
                await Task.Delay(2000, _speedRunCts.Token);
            }

            // Step 2: Subscribe to profile mods
            SpeedRunStatus = "Subscribing to profile mods...";
            _mainVm.StatusMessage = "[Speed Run] Subscribing to profile mods...";
            await _steamSessionService.SubscribeManyAsync(profileModIds);

            // Step 3: Launch game directly via exe
            SpeedRunStatus = "Launching PlateUp! with mods...";
            _mainVm.StatusMessage = "[Speed Run] Launching PlateUp! with mods...";
            _gameService.LaunchGameExe(_mainVm.Settings.GameFolderPath);

            // Step 4: Wait for game to exit
            SpeedRunStatus = "Waiting for PlateUp! to close...";
            _mainVm.StatusMessage = "[Speed Run] Waiting for PlateUp! to close...";
            await _gameService.WaitForGameToExitAsync(_speedRunCts.Token);

            // Step 5: Unsubscribe from all profile mods
            SpeedRunStatus = "Unsubscribing from mods...";
            _mainVm.StatusMessage = "[Speed Run] Unsubscribing from mods...";
            await _steamSessionService.UnsubscribeManyAsync(profileModIds);

            // Step 6: Relaunch clean directly via exe
            SpeedRunStatus = "Relaunching PlateUp! without mods...";
            _mainVm.StatusMessage = "[Speed Run] Relaunching PlateUp! without mods...";
            await Task.Delay(1000, _speedRunCts.Token);
            _gameService.LaunchGameExe(_mainVm.Settings.GameFolderPath);

            SpeedRunStatus = "Speed run mode complete!";
            _mainVm.StatusMessage = "[Speed Run] Complete!";
        }
        catch (OperationCanceledException)
        {
            SpeedRunStatus = "Speed run mode cancelled";
            await _steamSessionService.UnsubscribeManyAsync(profileModIds);
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
    public bool IsDefault => Profile.IsDefault;
    public int ModCount => Profile.Mods.Count;
    public string ModSummary => $"{ModCount} mod{(ModCount == 1 ? "" : "s")}";
    public string UpdatedAt => Profile.UpdatedAt.ToLocalTime().ToString("g");
    public string ExpandLabel => IsExpanded ? "▾ Hide Mods" : "▸ Show Mods";

    public List<ProfileMod> Mods => Profile.Mods;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandLabel))]
    private bool _isExpanded;

    public ProfileItemViewModel(ModProfile profile)
    {
        Profile = profile;
    }

    [RelayCommand]
    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }

    [RelayCommand]
    private static void OpenWorkshopPage(ProfileMod? mod)
    {
        if (mod == null) return;
        var url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.PublishedFileId}";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
