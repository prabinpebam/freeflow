using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;

namespace FreeFlow.Core.Abstractions;

/// <summary>Microphone capture seam. Faked with fixture audio in the inner loop.</summary>
public interface IAudioCaptureService
{
    Task StartAsync(CancellationToken ct = default);
    Task<AudioClip> StopAndGetClipAsync(CancellationToken ct = default);
}

/// <summary>
/// Optional companion to <see cref="IAudioCaptureService"/> that publishes a
/// smoothed 0..1 microphone level while recording, so the UI can drive a live
/// meter (e.g. the recording overlay). Kept separate from the capture seam so
/// the deterministic inner-loop fakes need not implement it.
/// </summary>
public interface IAudioLevelMonitor
{
    /// <summary>Raised with a normalized 0..1 input level as audio is captured.</summary>
    event Action<float>? LevelChanged;
}

/// <summary>
/// Enumerates the available microphone (audio input) devices so the settings UI
/// can offer a selection. Backed by the platform audio stack; not used in the
/// deterministic inner loop.
/// </summary>
public interface IAudioDeviceProvider
{
    IReadOnlyList<AudioInputDevice> GetInputDevices();
}

/// <summary>Speech-to-text seam. Faked with cassettes in the inner loop.</summary>
public interface ITranscriptionClient
{
    Task<string> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default);
}

/// <summary>LLM cleanup seam. Faked/scripted in the inner loop.</summary>
public interface IPostProcessingClient
{
    Task<string> CleanupAsync(PostProcessingRequest request, CancellationToken ct = default);
}

/// <summary>Foreground app/window context seam.</summary>
public interface IContextService
{
    Task<CaptureContext> CaptureAsync(CancellationToken ct = default);
}

/// <summary>Clipboard preserve/paste/restore seam.</summary>
public interface IClipboardPasteService
{
    Task PasteTextAsync(string text, CancellationToken ct = default);
}

/// <summary>Identifier seam so run ids are deterministic under test.</summary>
public interface IIdProvider
{
    Guid NewId();
}

public sealed class SystemIdProvider : IIdProvider
{
    public Guid NewId() => Guid.NewGuid();
}
