using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellfireStudios.PlateupModManager.Models;
using HellfireStudios.PlateupModManager.Services;

namespace HellfireStudios.PlateupModManager.UI.ViewModels;

public partial class WorkshopBrowserViewModel : ObservableObject
{
    private readonly SteamWorkshopService _workshopService;
    private readonly MainViewModel _mainVm;
    private readonly ImageCacheService _imageCache = new();

    private List<WorkshopMod> _allItems = [];
    private bool _hasLoaded;
    private CancellationTokenSource? _imageCacheCts;

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

    [ObservableProperty]
    private string _loadingProgress = string.Empty;

    private const int ItemsPerPage = 30;

    public ObservableCollection<WorkshopMod> WorkshopItems { get; } = [];
    public ObservableCollection<string> SearchSuggestions { get; } = [];

    public WorkshopBrowserViewModel(SteamWorkshopService workshopService, MainViewModel mainVm)
    {
        _workshopService = workshopService;
        _mainVm = mainVm;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
        UpdateSuggestions();
    }

    /// <summary>
    /// Called when the view is navigated to. Loads all mods if not already loaded.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (_hasLoaded) return;
        await LoadAllModsAsync();
    }

    [RelayCommand]
    private async Task LoadAllModsAsync()
    {
        if (string.IsNullOrEmpty(_mainVm.Settings.SteamApiKey))
        {
            ErrorMessage = "Please set your Steam API Key in Settings first.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        _allItems.Clear();
        WorkshopItems.Clear();

        try
        {
            var page = 1;
            int totalFetched = 0;
            int totalAvailable;

            do
            {
                var result = await _workshopService.QueryWorkshopItemsAsync(
                    _mainVm.Settings.SteamApiKey,
                    searchText: null,
                    page: page,
                    perPage: 100);

                totalAvailable = result.Total;
                _allItems.AddRange(result.Items);
                totalFetched += result.Items.Count;

                LoadingProgress = $"Loading mods... {totalFetched}/{totalAvailable}";
                _mainVm.StatusMessage = LoadingProgress;

                page++;

                // Safety: break if no more items returned
                if (result.Items.Count == 0) break;

            } while (totalFetched < totalAvailable);

            TotalResults = _allItems.Count;
            _hasLoaded = true;
            _mainVm.StatusMessage = $"Loaded {TotalResults} workshop mods";

            CurrentPage = 1;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading workshop: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            LoadingProgress = string.Empty;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            ApplyFilter();
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            ApplyFilter();
        }
    }

    [RelayCommand]
    private void OpenInSteam(WorkshopMod? mod)
    {
        if (mod == null) return;
        _workshopService.OpenWorkshopPageInSteam(mod.PublishedFileId);
    }

    [RelayCommand]
    private void OpenInBrowser(WorkshopMod? mod)
    {
        if (mod == null) return;
        _workshopService.OpenWorkshopPageInBrowser(mod.PublishedFileId);
    }

    private void ApplyFilter()
    {
        var filtered = _allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            filtered = filtered.Where(m =>
                m.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                m.PublishedFileId.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var filteredList = filtered.ToList();
        TotalResults = filteredList.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(filteredList.Count / (double)ItemsPerPage));

        if (CurrentPage > TotalPages)
            CurrentPage = TotalPages;

        var pageItems = filteredList
            .Skip((CurrentPage - 1) * ItemsPerPage)
            .Take(ItemsPerPage);

        WorkshopItems.Clear();
        foreach (var item in pageItems)
        {
            WorkshopItems.Add(item);
        }

        // Pre-cache thumbnails for this page in the background
        PreCacheCurrentPageImages();
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
                // Refresh the list on the UI thread to pick up cached images
                System.Windows.Application.Current?.Dispatcher.Invoke(RefreshCurrentItems);
            }
        }, token);
    }

    private void RefreshCurrentItems()
    {
        // Re-set the same items to force WPF to re-evaluate the image converter
        var items = WorkshopItems.ToList();
        WorkshopItems.Clear();
        foreach (var item in items)
        {
            WorkshopItems.Add(item);
        }
    }

    private void UpdateSuggestions()
    {
        SearchSuggestions.Clear();

        if (string.IsNullOrWhiteSpace(SearchText) || SearchText.Length < 2)
            return;

        var matches = _allItems
            .Where(m => m.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Title)
            .Distinct()
            .Take(8);

        foreach (var title in matches)
        {
            SearchSuggestions.Add(title);
        }
    }
}
