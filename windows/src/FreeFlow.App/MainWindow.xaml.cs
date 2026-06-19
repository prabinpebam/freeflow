using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace FreeFlow_App;

/// <summary>
/// The application window. This hosts a Frame that displays pages. Add your
/// UI and logic to MainPage.xaml / MainPage.xaml.cs instead of here so you
/// can use Page features such as navigation events and the Loaded lifecycle.
/// </summary>
public sealed partial class MainWindow : Window
{
    // Sensible default window size (in DIPs). Without this the unpackaged
    // window opens at the OS default, which is excessively wide for this
    // compact settings/dictation shell.
    private const int DefaultWidthDips = 480;
    private const int DefaultHeightDips = 760;

    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        // Closing the window hides FreeFlow to the tray instead of exiting, so
        // the global shortcuts keep working in the background (like the macOS
        // menu-bar app). A real exit goes through the tray's Quit command.
        AppWindow.Closing += OnAppWindowClosing;

        ResizeToDefault();

        // Navigate the root frame to the main page on startup.
        RootFrame.Navigate(typeof(MainPage));
    }

    /// <summary>Navigates the root frame straight to the Settings page (first run).</summary>
    public void NavigateToSettings() => RootFrame.Navigate(typeof(SettingsPage));

    /// <summary>Navigates the root frame to the first-run onboarding wizard.</summary>
    public void NavigateToOnboarding() => RootFrame.Navigate(typeof(OnboardingPage));

    /// <summary>Brings the window back from the tray and focuses it.</summary>
    public void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    /// <summary>Permits the next close to actually exit (used by the tray Quit command).</summary>
    public void AllowClose() => _allowClose = true;

    private void OnAppWindowClosing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        AppWindow.Hide();
    }

    /// <summary>
    /// Sets a fixed, DPI-aware default window size. AppWindow.Resize takes
    /// physical pixels, so the DIP defaults are scaled by the window's DPI to
    /// look consistent across 100%/150%/200% displays.
    /// </summary>
    private void ResizeToDefault()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var dpi = GetDpiForWindow(hwnd);
        var scale = dpi <= 0 ? 1.0 : dpi / 96.0;

        AppWindow.Resize(new SizeInt32(
            (int)(DefaultWidthDips * scale),
            (int)(DefaultHeightDips * scale)));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
