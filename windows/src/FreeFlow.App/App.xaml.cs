using FreeFlow.Core.Input;
using FreeFlow.Core.Pipeline;
using FreeFlow.Platform.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FreeFlow_App;

/// <summary>
/// Application composition root. Builds the dependency-injection container
/// (Core pipeline wired against Phase-2 platform services) and launches the
/// main window. Real platform adapters replace the mock registrations in later
/// phases without changing this file.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private DictationCoordinator? _coordinator;
    private IHotkeyService? _hotkeys;

    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        Services = BuildServices();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Start the global keyboard hook on the UI thread (it owns the message
        // loop the WH_KEYBOARD_LL hook requires) and keep the coordinator alive
        // so the configured shortcuts drive the pipeline end-to-end.
        _coordinator = Services.GetRequiredService<DictationCoordinator>();
        _hotkeys = Services.GetRequiredService<IHotkeyService>();
        _hotkeys.Start();

        _window = new MainWindow();
        _window.Closed += (_, _) => _hotkeys?.Stop();
        _window.Activate();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddFreeFlowMockPlatform();
        return services.BuildServiceProvider();
    }
}
