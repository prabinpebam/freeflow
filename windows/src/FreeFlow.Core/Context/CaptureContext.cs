namespace FreeFlow.Core.Context;

/// <summary>
/// Foreground application metadata captured at the moment dictation starts.
/// The Windows analog of the macOS app/bundle/window context.
/// </summary>
public sealed record CaptureContext(
    string? AppName,
    string? ProcessName,
    string? WindowTitle,
    string? SelectedText)
{
    public static readonly CaptureContext None = new(null, null, null, null);

    public bool HasSelection => !string.IsNullOrEmpty(SelectedText);
}
