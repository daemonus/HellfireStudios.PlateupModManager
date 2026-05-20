using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using HellfireStudios.PlateupModManager.UI.ViewModels;

namespace HellfireStudios.PlateupModManager.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyDarkTitleBar();
        await _viewModel.InitializeAsync();
    }

    // ── Dark Title Bar ──────────────────────────────────────────────────

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;

    private void ApplyDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Enable dark mode for title bar buttons (close/min/max)
            var darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Set title bar background color to match app theme (#1e1e2e → BGR: 0x2E1E1E)
            var captionColor = 0x2E1E1E; // BGR format: B=0x2E, G=0x1E, R=0x1E
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
        }
        catch
        {
            // Silently ignore on older Windows versions that don't support these attributes
        }
    }
}
