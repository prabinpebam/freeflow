namespace FreeFlow.Core.Abstractions;

/// <summary>
/// Registers (or removes) FreeFlow from the per-user startup so it launches when
/// the user signs in — the Windows equivalent of the macOS "Launch at login"
/// toggle (<c>SMAppService</c>). Implemented in the platform layer over the
/// registry Run key; the toggle in settings is wired to this seam.
/// </summary>
public interface ILaunchAtLoginService
{
    /// <summary>True when FreeFlow is currently registered to start at login.</summary>
    bool IsEnabled();

    /// <summary>Adds or removes the per-user startup registration.</summary>
    void SetEnabled(bool enabled);
}

/// <summary>Distinct audio cues the app can play, mirroring the macOS alert sounds.</summary>
public enum AppSound
{
    RecordingStarted,
    RecordingStopped,
    TranscriptReady,
    Error,
}

/// <summary>
/// Plays short, non-blocking audio cues for recording lifecycle events. Gated by
/// the General &gt; play sounds preference at the call site. Backed by the Win32
/// sound APIs in the platform layer.
/// </summary>
public interface ISoundService
{
    void Play(AppSound sound);
}
