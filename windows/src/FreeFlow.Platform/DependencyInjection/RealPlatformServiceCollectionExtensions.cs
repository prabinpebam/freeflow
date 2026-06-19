using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Context;
using FreeFlow.Core.History;
using FreeFlow.Core.Input;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Providers;
using FreeFlow.Core.Settings;
using FreeFlow.Infrastructure.History;
using FreeFlow.Infrastructure.Providers;
using FreeFlow.Infrastructure.Settings;
using FreeFlow.Platform.Audio;
using FreeFlow.Platform.Clipboard;
using FreeFlow.Platform.Context;
using FreeFlow.Platform.Input;
using FreeFlow.Platform.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FreeFlow.Platform.DependencyInjection;

/// <summary>
/// Wires the dictation pipeline against the <b>real</b> Windows adapters:
/// NAudio capture, OpenAI-compatible HTTP providers, Win32 foreground context,
/// clipboard preserve/paste/restore, a low-level keyboard hook, and JSON history
/// persistence. This is the composition the shipping app uses; the inner loop
/// keeps using <c>AddFreeFlowMockPlatform</c> + fakes.
/// </summary>
public static class RealPlatformServiceCollectionExtensions
{
    /// <summary>Default per-user history file for the unpackaged app.</summary>
    public static string DefaultHistoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FreeFlow",
        "history.json");

    /// <summary>Default per-user settings file for the unpackaged app.</summary>
    public static string DefaultSettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FreeFlow",
        "settings.json");

    public static IServiceCollection AddFreeFlowRealPlatform(
        this IServiceCollection services,
        ProviderConfiguration? providers = null,
        HotkeyBindings? bindings = null,
        string? historyPath = null)
    {
        if (!services.Any(d => d.ServiceType == typeof(ILoggerFactory)))
        {
            services.AddFreeFlowLogging();
        }

        var configuration = providers ?? new ProviderConfiguration();
        var hotkeys = bindings ?? HotkeyBindings.Defaults;

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IIdProvider, SystemIdProvider>();

        services.AddSingleton<IAudioCaptureService>(sp =>
            new NAudioCaptureService(sp.GetService<ILogger<NAudioCaptureService>>()));

        services.AddSingleton<ITranscriptionClient>(sp =>
            new HttpTranscriptionClient(
                CreateHttpClient(configuration.Transcription.Timeout),
                configuration.Transcription,
                sp.GetService<ILogger<HttpTranscriptionClient>>()));

        services.AddSingleton<IPostProcessingClient>(sp =>
            new HttpPostProcessingClient(
                CreateHttpClient(configuration.PostProcessing.Timeout),
                configuration.PostProcessing,
                sp.GetService<ILogger<HttpPostProcessingClient>>()));

        services.AddSingleton<IContextService, ForegroundWindowContextService>();
        services.AddSingleton<ISelectionReader>(sp =>
            new ClipboardSelectionReader(sp.GetService<ILogger<ClipboardSelectionReader>>()));
        services.AddSingleton<IClipboardPasteService>(sp =>
            new Win32ClipboardPasteService(sp.GetService<ILogger<Win32ClipboardPasteService>>()));

        services.AddSingleton<IHistoryStore>(sp =>
            new JsonHistoryStore(
                historyPath ?? DefaultHistoryPath,
                cap: 200,
                sp.GetService<ILogger<JsonHistoryStore>>()));

        services.AddSingleton<IHotkeyService>(sp =>
        {
            var svc = new KeyboardHookHotkeyService(sp.GetService<ILogger<KeyboardHookHotkeyService>>());
            svc.Configure(hotkeys);
            return svc;
        });

        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<ISettingsStore>(sp =>
            new JsonSettingsStore(
                DefaultSettingsPath,
                sp.GetRequiredService<ISecretProtector>(),
                sp.GetService<ILogger<JsonSettingsStore>>()));

        services.AddSingleton<DictationPipeline>();
        services.AddSingleton<DictationCoordinator>();

        return services;
    }

    private static HttpClient CreateHttpClient(TimeSpan timeout)
        => new() { Timeout = timeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(30) : timeout };
}
