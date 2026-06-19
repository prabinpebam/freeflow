namespace FreeFlow.Core.Input;

/// <summary>
/// The three dictation shortcuts, mirroring the macOS scheme (ADR-009): a
/// hold-to-talk binding, a toggle binding, and a paste-again binding.
/// </summary>
public sealed record HotkeyBindings(
    HotkeyCombination HoldToTalk,
    HotkeyCombination Toggle,
    HotkeyCombination PasteAgain)
{
    /// <summary>
    /// Windows defaults (ADR-009): hold Right Ctrl, toggle Ctrl+Alt+Space,
    /// paste-again Ctrl+Alt+V. <c>Fn</c> is not interceptable on Windows.
    /// </summary>
    public static readonly HotkeyBindings Defaults = new(
        HotkeyCombination.Parse("RightCtrl"),
        HotkeyCombination.Parse("Ctrl+Alt+Space"),
        HotkeyCombination.Parse("Ctrl+Alt+V"));
}

/// <summary>Why the hotkey service raised an activation.</summary>
public enum HotkeyTrigger
{
    HoldStart,
    HoldStop,
    Toggle,
    PasteAgain,
}

/// <summary>
/// Global keyboard shortcut seam. The real adapter installs a low-level keyboard
/// hook; the inner-loop fake raises events directly so press/hold/release timing
/// is fully scripted and deterministic.
/// </summary>
public interface IHotkeyService
{
    /// <summary>Apply (or re-apply) the active bindings.</summary>
    void Configure(HotkeyBindings bindings);

    /// <summary>Begin listening for the configured shortcuts.</summary>
    void Start();

    /// <summary>Stop listening and release any OS hooks.</summary>
    void Stop();

    /// <summary>Raised when a configured shortcut activates.</summary>
    event Action<HotkeyTrigger>? Triggered;
}
