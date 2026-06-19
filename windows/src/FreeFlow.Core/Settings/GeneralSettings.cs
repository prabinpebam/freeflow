namespace FreeFlow.Core.Settings;

/// <summary>
/// Application-wide preferences that do not influence the transcription pipeline
/// itself (mirrors the macOS "General" settings tab). Kept platform-agnostic so
/// the settings round-trip and validation are deterministically testable.
/// </summary>
public sealed record GeneralSettings
{
    /// <summary>Start FreeFlow automatically when the user signs in.</summary>
    public bool LaunchAtLogin { get; init; }

    /// <summary>
    /// Persisted microphone selection. Stores the device id from
    /// <see cref="FreeFlow.Core.Audio.AudioInputDevice"/>; empty means the OS
    /// default capture device. Resolved to a live device at capture time, so an
    /// unplugged device transparently falls back to the default.
    /// </summary>
    public string InputDeviceId { get; init; } = string.Empty;

    /// <summary>Play start/stop cues while recording.</summary>
    public bool PlaySounds { get; init; } = true;

    /// <summary>Show the floating recording overlay.</summary>
    public bool ShowOverlay { get; init; } = true;

    /// <summary>Maximum number of dictation runs kept in history.</summary>
    public int HistoryCap { get; init; } = 200;
}
