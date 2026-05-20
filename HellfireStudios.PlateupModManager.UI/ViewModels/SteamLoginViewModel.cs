using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HellfireStudios.PlateupModManager.Services;

namespace HellfireStudios.PlateupModManager.UI.ViewModels;

public partial class SteamLoginViewModel : ObservableObject
{
    private readonly SteamSessionService _sessionService;
    private readonly MainViewModel _mainVm;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _steamId = string.Empty;

    [ObservableProperty]
    private string _statusText = "Sign in with your Steam account to enable mod subscription management.";

    [ObservableProperty]
    private bool _isValidating;

    /// <summary>
    /// Raised when cookies have been captured and the login view can be hidden.
    /// </summary>
    public event Action? LoginCompleted;

    public SteamSessionService SessionService => _sessionService;

    public SteamLoginViewModel(SteamSessionService sessionService, MainViewModel mainVm)
    {
        _sessionService = sessionService;
        _mainVm = mainVm;
        RefreshLoginState();
    }

    /// <summary>
    /// Called by the view's code-behind when WebView2 navigates to a page after login.
    /// The view extracts cookies and passes them here.
    /// </summary>
    public async Task OnCookiesCapturedAsync(string sessionId, string steamLoginSecure)
    {
        IsValidating = true;
        StatusText = "Validating session...";

        _sessionService.SetSessionFromCookies(sessionId, steamLoginSecure);

        var valid = await _sessionService.ValidateSessionAsync();
        if (valid)
        {
            await _sessionService.SaveSessionAsync();
            RefreshLoginState();
            StatusText = $"Signed in as {DisplayName}";
            _mainVm.StatusMessage = $"Steam: signed in as {DisplayName}";
            LoginCompleted?.Invoke();
        }
        else
        {
            StatusText = "Session validation failed. Please try again.";
        }

        IsValidating = false;
    }

    [RelayCommand]
    private void Logout()
    {
        _sessionService.Logout();
        RefreshLoginState();
        StatusText = "Signed out. Sign in again to manage subscriptions.";
        _mainVm.StatusMessage = "Steam: signed out";
    }

    public async Task TryRestoreSessionAsync()
    {
        var loaded = await _sessionService.TryLoadSessionAsync();
        if (loaded)
        {
            IsValidating = true;
            StatusText = "Restoring previous session...";

            var valid = await _sessionService.ValidateSessionAsync();
            if (valid)
            {
                await _sessionService.SaveSessionAsync();
                RefreshLoginState();
                StatusText = $"Signed in as {DisplayName}";
            }
            else
            {
                _sessionService.Logout();
                StatusText = "Previous session expired. Please sign in again.";
            }

            IsValidating = false;
        }

        RefreshLoginState();
    }

    private void RefreshLoginState()
    {
        IsLoggedIn = _sessionService.IsLoggedIn;
        DisplayName = _sessionService.DisplayName ?? string.Empty;
        SteamId = _sessionService.SteamId ?? string.Empty;
        _mainVm.OnSteamLoginStateChanged();
    }
}
