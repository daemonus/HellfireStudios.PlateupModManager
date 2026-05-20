using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellfireStudios.PlateupModManager.Models;
using HellfireStudios.PlateupModManager.Services;

namespace HellfireStudios.PlateupModManager.UI.ViewModels;

public partial class WorkshopBrowserViewModel : ObservableObject
{
    private readonly SteamWorkshopService _workshopService;
    private readonly SteamSessionService _steamSessionService;
    private readonly MainViewModel _mainVm;
    private readonly ImageCacheService _imageCache = new();

    private CancellationTokenSource? _imageCacheCts;
    private CancellationTokenSource? _searchCts;
    private string _lastSearchText = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _totalResults;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    private const int ItemsPerPage = 30;

    public ObservableCollection<WorkshopMod> WorkshopItems { get; } = [];

    public WorkshopBrowserViewModel(SteamWorkshopService workshopService, SteamSessionService steamSessionService, MainViewModel mainVm)
    {
        _workshopService = workshopService;
        _steamSessionService = steamSessionService;
        _mainVm = mainVm;
    }

    /// <summary>
    /// Called when the view is navigated to. Loads page 1 if not already loaded.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (WorkshopItems.Count > 0) return;
        await FetchPageAsync();
    }

    /// <summary>
    /// Fetches a single page of results from Steam, using server-side search and pagination.
    /// </summary>
    private async Task FetchPageAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
            var result = await _workshopService.QueryWorkshopItemsAsync(
                searchText: search,
                page: CurrentPage,
                perPage: ItemsPerPage);

            TotalResults = result.Total;
            TotalPages = Math.Max(1, (int)Math.Ceiling(result.Total / (double)ItemsPerPage));

            WorkshopItems.Clear();
            foreach (var item in result.Items)
            {
                WorkshopItems.Add(item);
            }

            _mainVm.StatusMessage = search != null
                ? $"Found {TotalResults} results for \"{search}\" (page {CurrentPage}/{TotalPages})"
                : $"Showing {TotalResults} workshop mods (page {CurrentPage}/{TotalPages})";

            PreCacheCurrentPageImages();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading workshop: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1;
        await FetchPageAsync();
    }

    [RelayCommand]
    private async Task ClearSearchAsync()
    {
        SearchText = string.Empty;
        CurrentPage = 1;
        await FetchPageAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        // Debounced server-side search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(async () =>
        {
            await Task.Delay(500, token);
            if (!token.IsCancellationRequested)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    CurrentPage = 1;
                    await FetchPageAsync();
                });
            }
        }, token);
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            await FetchPageAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            await FetchPageAsync();
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        await FetchPageAsync();
    }

    [RelayCommand]
    private void OpenInSteam(WorkshopMod? mod)
    {
        if (mod == null) return;
        _workshopService.OpenWorkshopPageInSteam(mod.PublishedFileId);
    }

    [RelayCommand]
    private async Task SubscribeModAsync(WorkshopMod? mod)
    {
        if (mod == null) return;

        if (!_steamSessionService.IsLoggedIn)
        {
            _mainVm.StatusMessage = "Sign in to your Steam account first (🔑 Steam Account)";
            return;
        }

        _mainVm.StatusMessage = $"Subscribing to '{mod.Title}'...";
        var success = await _steamSessionService.SubscribeAsync(mod.PublishedFileId);
        if (!success)
        {
            _mainVm.StatusMessage = $"Failed to subscribe to '{mod.Title}'. Session may have expired.";
            return;
        }

        // Check for and subscribe to dependencies
        var deps = await _workshopService.GetDependenciesAsync(mod.PublishedFileId);
        if (deps.Count > 0)
        {
            _mainVm.StatusMessage = $"Subscribing to {deps.Count} dependencies for '{mod.Title}'...";
            await _steamSessionService.SubscribeManyAsync(deps);
            _mainVm.StatusMessage = $"Subscribed to '{mod.Title}' + {deps.Count} dependencies — Steam will download shortly";
        }
        else
        {
            _mainVm.StatusMessage = $"Subscribed to '{mod.Title}' — Steam will download it shortly";
        }
    }

    private void PreCacheCurrentPageImages()
    {
        _imageCacheCts?.Cancel();
        _imageCacheCts = new CancellationTokenSource();
        var token = _imageCacheCts.Token;

        var urls = WorkshopItems
            .Where(m => !string.IsNullOrEmpty(m.PreviewUrl) && _imageCache.GetCachedPath(m.PreviewUrl) == null)
            .Select(m => m.PreviewUrl)
            .ToList();

        if (urls.Count == 0) return;

        _ = Task.Run(async () =>
        {
            var tasks = urls.Select(url => _imageCache.GetOrDownloadAsync(url));
            await Task.WhenAll(tasks);

            if (!token.IsCancellationRequested)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(RefreshCurrentItems);
            }
        }, token);
    }

    private void RefreshCurrentItems()
    {
        var items = WorkshopItems.ToList();
        WorkshopItems.Clear();
        foreach (var item in items)
        {
            WorkshopItems.Add(item);
        }
    }
}
