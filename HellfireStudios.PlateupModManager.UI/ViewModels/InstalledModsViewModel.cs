using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellfireStudios.PlateupModManager.Services;

namespace HellfireStudios.PlateupModManager.UI.ViewModels;

public partial class InstalledModsViewModel : ObservableObject
{
    private readonly ModManagerService _modManagerService;
    private readonly SteamSessionService _steamSessionService;
    private readonly ProfileService _profileService;
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private string _saveProfileName = string.Empty;

    [ObservableProperty]
    private string _saveProfileDescription = string.Empty;

    public ObservableCollection<InstalledModItemViewModel> Mods { get; } = [];

    public bool IsSteamLoggedIn => _steamSessionService.IsLoggedIn;

    public InstalledModsViewModel(ModManagerService modManagerService, SteamSessionService steamSessionService, ProfileService profileService, MainViewModel mainVm)
    {
        _modManagerService = modManagerService;
        _steamSessionService = steamSessionService;
        _profileService = profileService;
        _mainVm = mainVm;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var workshopPath = _mainVm.Settings.WorkshopFolderPath;
        if (string.IsNullOrEmpty(workshopPath)) return;

        var installed = _modManagerService.GetInstalledMods(workshopPath);
        Mods.Clear();

        foreach (var mod in installed.OrderBy(m => m.PublishedFileId))
        {
            var vm = new InstalledModItemViewModel(mod, this);
            Mods.Add(vm);
        }

        _mainVm.StatusMessage = $"{installed.Count} mods in workshop folder";

        // Resolve titles from Steam Web API in the background
        var titles = await _modManagerService.ResolveModTitlesAsync(
            installed.Select(m => m.PublishedFileId));

        foreach (var modVm in Mods)
        {
            if (titles.TryGetValue(modVm.PublishedFileId, out var title))
                modVm.Title = title;
        }
    }

    [RelayCommand]
    private async Task UnsubscribeModAsync(InstalledModItemViewModel? modVm)
    {
        if (modVm == null) return;

        if (!_steamSessionService.IsLoggedIn)
        {
            _mainVm.StatusMessage = "Sign in to your Steam account first (🔑 Steam Account)";
            return;
        }

        _mainVm.StatusMessage = $"Unsubscribing from '{modVm.Title}'...";
        var success = await _steamSessionService.UnsubscribeAsync(modVm.PublishedFileId);
        if (success)
        {
            Mods.Remove(modVm);
            _mainVm.StatusMessage = $"Unsubscribed from '{modVm.Title}'";
        }
        else
        {
            _mainVm.StatusMessage = $"Failed to unsubscribe from '{modVm.Title}'. Session may have expired.";
        }
    }

    [RelayCommand]
    private async Task UnsubscribeAllAsync()
    {
        if (!_steamSessionService.IsLoggedIn)
        {
            _mainVm.StatusMessage = "Sign in to your Steam account first (🔑 Steam Account)";
            return;
        }

        var modIds = Mods.Select(m => m.PublishedFileId).ToList();
        _mainVm.StatusMessage = $"Unsubscribing from {modIds.Count} mods...";
        var count = await _steamSessionService.UnsubscribeManyAsync(modIds);
        Mods.Clear();
        _mainVm.StatusMessage = $"Unsubscribed from {count} mods";
    }

    [RelayCommand]
    private async Task SaveAsProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(SaveProfileName))
        {
            _mainVm.StatusMessage = "Please enter a profile name";
            return;
        }

        var installedMods = Mods.Select(m => new InstalledMod
        {
            PublishedFileId = m.PublishedFileId,
            Title = m.Title
        });

        var profile = await _profileService.CreateProfileFromModsAsync(
            SaveProfileName,
            SaveProfileDescription,
            installedMods);

        SaveProfileName = string.Empty;
        SaveProfileDescription = string.Empty;
        _mainVm.StatusMessage = $"Profile '{profile.Name}' saved with {profile.Mods.Count} mods";
    }
}

public partial class InstalledModItemViewModel : ObservableObject
{
    private readonly InstalledModsViewModel _parent;

    public string PublishedFileId { get; }

    [ObservableProperty]
    private string _title;

    public InstalledModItemViewModel(InstalledMod mod, InstalledModsViewModel parent)
    {
        _parent = parent;
        PublishedFileId = mod.PublishedFileId;
        _title = string.IsNullOrEmpty(mod.Title) ? mod.PublishedFileId : mod.Title;
    }
}
