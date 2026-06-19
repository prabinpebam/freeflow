using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Input;
using System;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;
using FreeFlow.Infrastructure.Settings;
using FreeFlow.Platform.DependencyInjection;
using FreeFlow.Platform.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

namespace FreeFlow_App;

/// <summary>
/// Application composition root. Loads persisted <see cref="AppSettings"/>, wires
/// the dictation pipeline against the <b>real</b> Windows platform adapters
/// (NAudio capture, OpenAI-compatible HTTP providers, Win32 clipboard paste, a
/// low-level global keyboard hook, UI-Automation selection), starts the global
/// shortcuts, and shows the main window. On first run (no transcription API key)
/// it opens straight to Settings so the user can paste their key.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private DictationCoordinator? _coordinator;
    private IHotkeyService? _hotkeys;
    private OverlayController? _overlay;

    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        Services = BuildServices();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            // Start the global keyboard hook on the UI thread (it owns the message
            // loop the WH_KEYBOARD_LL hook requires) and keep the coordinator alive
            // so the configured shortcuts drive the real pipeline end-to-end.
            _coordinator = Services.GetRequiredService<DictationCoordinator>();
            _hotkeys = Services.GetRequiredService<IHotkeyService>();
            _hotkeys.Start();

            var main = new MainWindow();
            _window = main;

            // Recording overlay: live mic meter while recording, "Transcribing…"
            // while processing. Shown without activation so it never steals focus
            // from the app being dictated into. Honors the General > overlay toggle.
            var settingsStore = Services.GetRequiredService<ISettingsStore>();
            _overlay = new OverlayController(
                Services.GetRequiredService<DictationPipeline>(),
                Services.GetService<IAudioLevelMonitor>(),
                main.DispatcherQueue,
                () => settingsStore.Load().General.ShowOverlay);

            _window.Closed += (_, _) =>
            {
                _hotkeys?.Stop();
                _overlay?.Dispose();
            };
            _window.Activate();

            // First-run experience: if no transcription credentials are configured,
            // jump straight to Settings so the app is usable immediately.
            var settings = settingsStore.Load();
            if (!settings.Providers.Transcription.HasCredentials)
            {
                main.NavigateToSettings();
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FreeFlow");
            System.IO.Directory.CreateDirectory(dir);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(dir, "crash.log"),
                $"[{DateTime.UtcNow:o}] FATAL OnLaunched exception{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Never let crash logging mask the original failure.
        }
    }

    private static IServiceProvider BuildServices()
    {
        // Read persisted settings up-front (outside the container) so the real
        // adapters can be built from the user's providers, shortcuts, and
        // dictation preferences. A missing/corrupt file loads as defaults.
        var protector = new DpapiSecretProtector();
        var store = new JsonSettingsStore(
            RealPlatformServiceCollectionExtensions.DefaultSettingsPath,
            protector);
        var settings = store.Load();

        var services = new ServiceCollection();
        services.AddFreeFlowRealPlatform(
            providers: settings.Providers,
            bindings: settings.Hotkeys,
            historyPath: null,
            dictation: settings.Dictation);
        return services.BuildServiceProvider();
    }
}
