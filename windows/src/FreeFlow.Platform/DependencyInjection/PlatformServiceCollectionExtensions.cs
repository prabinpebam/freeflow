using FreeFlow.Core.Abstractions;
using FreeFlow.Core.History;
using FreeFlow.Core.Input;
using FreeFlow.Core.Pipeline;
using FreeFlow.Infrastructure.History;
using FreeFlow.Platform.Clipboard;
using FreeFlow.Platform.Context;
using FreeFlow.Platform.Input;
using FreeFlow.Platform.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FreeFlow.Platform.DependencyInjection;

/// <summary>
/// Wires the dictation pipeline against Phase-2 platform services: real
/// foreground-window context, mock audio/transcription/post-processing, and a
/// recording paste service. The real global keyboard hook and the
/// <see cref="DictationCoordinator"/> are included so the demo app exercises the
/// full input→pipeline vertical slice without needing provider credentials. Swap
/// individual registrations for real adapters via <c>AddFreeFlowRealPlatform</c>
/// as Phase 3 lands.
/// </summary>
public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddFreeFlowMockPlatform(this IServiceCollection services)
    {
        // Ensure the logging host exists so every service can take an ILogger<T>.
        // Skipped if the caller already configured logging (avoids duplicate sinks).
        if (!services.Any(d => d.ServiceType == typeof(ILoggerFactory)))
        {
            services.AddFreeFlowLogging();
        }

        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IIdProvider, SystemIdProvider>();

        services.AddSingleton<IAudioCaptureService, MockAudioCaptureService>();
        services.AddSingleton<ITranscriptionClient, MockTranscriptionClient>();
        services.AddSingleton<IPostProcessingClient, MockPostProcessingClient>();
        services.AddSingleton<IContextService, ForegroundWindowContextService>();
        services.AddSingleton<IHistoryStore>(sp =>
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FreeFlow");
            Directory.CreateDirectory(dir);
            return new JsonHistoryStore(
                Path.Combine(dir, "history.json"),
                logger: sp.GetService<ILogger<JsonHistoryStore>>());
        });

        services.AddSingleton<RecordingClipboardPasteService>();
        services.AddSingleton<IClipboardPasteService>(
            sp => sp.GetRequiredService<RecordingClipboardPasteService>());

        services.AddSingleton<IHotkeyService>(sp =>
        {
            var svc = new KeyboardHookHotkeyService(sp.GetService<ILogger<KeyboardHookHotkeyService>>());
            svc.Configure(HotkeyBindings.Defaults);
            return svc;
        });

        services.AddSingleton<DictationPipeline>();
        services.AddSingleton<DictationCoordinator>();

        return services;
    }
}
