using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellfireStudios.PlateupModManager.Services;

namespace HellfireStudios.PlateupModManager.UI.ViewModels;

public partial class InstalledModsViewModel : ObservableObject
{
    private readonly ModManagerService _modManagerService;
    private readonly ProfileService _profileService;
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private string _saveProfileName = string.Empty;

    [ObservableProperty]
    private string _saveProfileDescription = string.Empty;

    public ObservableCollection<InstalledModItemViewModel> Mods { get; } = [];

    public InstalledModsViewModel(ModManagerService modManagerService, ProfileService profileService, MainViewModel mainVm)
    {
        _modManagerService = modManagerService;
        _profileService = profileService;
        _mainVm = mainVm;
    }

    [RelayCommand]
    public void Refresh()
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
    }

    [RelayCommand]
    private void RemoveMod(InstalledModItemViewModel? modVm)
    {
        var workshopPath = _mainVm.Settings.WorkshopFolderPath;
        if (string.IsNullOrEmpty(workshopPath) || modVm == null) return;

        _modManagerService.RemoveMods(workshopPath, [modVm.PublishedFileId]);
        Refresh();
        _mainVm.StatusMessage = $"Removed mod '{modVm.Title}'";
    }

    [RelayCommand]
    private void RemoveAll()
    {
        var workshopPath = _mainVm.Settings.WorkshopFolderPath;
        if (string.IsNullOrEmpty(workshopPath)) return;

        var count = _modManagerService.RemoveAllMods(workshopPath);
        Refresh();
        _mainVm.StatusMessage = $"Removed {count} mods from workshop";
    }

    [RelayCommand]
    private async Task SaveAsProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(SaveProfileName))
        {
            _mainVm.StatusMessage = "Please enter a profile name";
            return;
        }

        var workshopPath = _mainVm.Settings.WorkshopFolderPath;
        if (string.IsNullOrEmpty(workshopPath)) return;

        var installedMods = Mods.Select(m => new InstalledMod
        {
            PublishedFileId = m.PublishedFileId,
            Title = m.Title,
            IsEnabled = true
        });

        var profile = await _profileService.CreateProfileFromModsAsync(
            SaveProfileName,
            SaveProfileDescription,
            workshopPath,
            _modManagerService,
            installedMods);

        SaveProfileName = string.Empty;
        SaveProfileDescription = string.Empty;
        _mainVm.StatusMessage = $"Profile '{profile.Name}' saved with {profile.Mods.Count} mods (files backed up)";
    }
}

public partial class InstalledModItemViewModel : ObservableObject
{
    private readonly InstalledModsViewModel _parent;

    public string PublishedFileId { get; }
    public string Title { get; }
    public string FolderPath { get; }

    [ObservableProperty]
    private bool _isEnabled;

    public InstalledModItemViewModel(InstalledMod mod, InstalledModsViewModel parent)
    {
        _parent = parent;
        PublishedFileId = mod.PublishedFileId;
        Title = string.IsNullOrEmpty(mod.Title) ? mod.PublishedFileId : mod.Title;
        FolderPath = mod.FolderPath;
        IsEnabled = mod.IsEnabled;
    }
}
