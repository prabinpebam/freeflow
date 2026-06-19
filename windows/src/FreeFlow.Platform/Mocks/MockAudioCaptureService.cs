using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Audio;

namespace FreeFlow.Platform.Mocks;

/// <summary>
/// Phase-2 placeholder for the real WASAPI capture adapter. Returns a fixed
/// one-second silent clip so the pipeline can run end-to-end without a mic.
/// Replaced by a real capture service in Phase 3.
/// </summary>
public sealed class MockAudioCaptureService : IAudioCaptureService
{
    private static readonly AudioClip SilentSecond = new(new byte[16000 * 2]);

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<AudioClip> StopAndGetClipAsync(CancellationToken ct = default)
        => Task.FromResult(SilentSecond);
}
