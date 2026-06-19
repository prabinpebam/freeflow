using FreeFlow.Core.Abstractions;

namespace FreeFlow.Platform.Clipboard;

/// <summary>
/// Phase-2 paste adapter that records what would be pasted and raises an event,
/// without driving the real clipboard or synthesizing keystrokes. This keeps the
/// demo safe (no focus stealing / SendInput) while exercising the full pipeline.
/// Replaced by a real clipboard preserve/paste/restore service in Phase 3.
/// </summary>
public sealed class RecordingClipboardPasteService : IClipboardPasteService
{
    public string? LastPasted { get; private set; }

    public event Action<string>? Pasted;

    public Task PasteTextAsync(string text, CancellationToken ct = default)
    {
        LastPasted = text;
        Pasted?.Invoke(text);
        return Task.CompletedTask;
    }
}
