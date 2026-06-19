using System.Runtime.InteropServices;
using FreeFlow.Core.Abstractions;

namespace FreeFlow.Platform.SystemIntegration;

/// <summary>
/// Plays short system audio cues via the Win32 <c>PlaySound</c> API using named
/// system sound aliases, so cues respect the user's Windows sound scheme and
/// play asynchronously without blocking the pipeline. Mirrors the macOS alert
/// sounds; gated by the General &gt; play sounds preference at the call site.
/// </summary>
public sealed class Win32SoundService : ISoundService
{
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_ALIAS = 0x00010000;
    private const uint SND_NODEFAULT = 0x0002;

    public void Play(AppSound sound)
    {
        var alias = sound switch
        {
            AppSound.RecordingStarted => "SystemAsterisk",
            AppSound.RecordingStopped => "SystemDefault",
            AppSound.TranscriptReady => "SystemNotification",
            AppSound.Error => "SystemHand",
            _ => "SystemDefault",
        };

        try
        {
            PlaySound(alias, IntPtr.Zero, SND_ALIAS | SND_ASYNC | SND_NODEFAULT);
        }
        catch
        {
            // Audio cues are best-effort; never let them disrupt a dictation run.
        }
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool PlaySound(string? pszSound, IntPtr hmod, uint fdwSound);
}
