using System.Windows.Controls;
using HellfireStudios.PlateupModManager.UI.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace HellfireStudios.PlateupModManager.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void LoginWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (DataContext is not SettingsViewModel settingsVm) return;
        var vm = settingsVm.SteamLoginVm;
        if (vm == null || vm.IsLoggedIn) return;

        try
        {
            var cookieManager = LoginWebView.CoreWebView2?.CookieManager;
            if (cookieManager == null) return;

            var cookies = await cookieManager.GetCookiesAsync("https://steamcommunity.com");

            string? sessionId = null;
            string? steamLoginSecure = null;

            foreach (var cookie in cookies)
            {
                switch (cookie.Name)
                {
                    case "sessionid":
                        sessionId = cookie.Value;
                        break;
                    case "steamLoginSecure":
                        steamLoginSecure = cookie.Value;
                        break;
                }
            }

            if (!string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(steamLoginSecure))
            {
                await vm.OnCookiesCapturedAsync(sessionId, steamLoginSecure);
            }
        }
        catch
        {
            // WebView2 may not be fully initialized yet
        }
    }
}
