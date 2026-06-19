using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Pipeline;
using FreeFlow.Platform.Clipboard;
using FreeFlow.Platform.Context;
using FreeFlow.Platform.Mocks;
using Microsoft.Extensions.DependencyInjection;

namespace FreeFlow.Platform.DependencyInjection;

/// <summary>
/// Wires the dictation pipeline against Phase-2 platform services: real
/// foreground-window context, mock audio/transcription/post-processing, and a
/// recording paste service. Swap individual registrations for real adapters as
/// Phase 3 lands without touching the App or Core.
/// </summary>
public static class PlatformServiceCollectionExtensions
{
    public static IServiceCollection AddFreeFlowMockPlatform(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IIdProvider, SystemIdProvider>();

        services.AddSingleton<IAudioCaptureService, MockAudioCaptureService>();
        services.AddSingleton<ITranscriptionClient, MockTranscriptionClient>();
        services.AddSingleton<IPostProcessingClient, MockPostProcessingClient>();
        services.AddSingleton<IContextService, ForegroundWindowContextService>();

        services.AddSingleton<RecordingClipboardPasteService>();
        services.AddSingleton<IClipboardPasteService>(
            sp => sp.GetRequiredService<RecordingClipboardPasteService>());

        services.AddSingleton<DictationPipeline>();

        return services;
    }
}
