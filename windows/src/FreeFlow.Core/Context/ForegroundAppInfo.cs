namespace FreeFlow.Core.Context;

/// <summary>
/// How the Platform layer can extract the foreground selection for command/edit mode.
/// </summary>
public enum SelectionStrategy
{
    /// <summary>UI Automation TextPattern (no side effects; preferred when available).</summary>
    UiAutomation,

    /// <summary>Synthesize Ctrl+C and read the clipboard (broad but has side effects).</summary>
    ClipboardCopy,
}

/// <summary>
/// Minimal description of the foreground application used to choose a safe
/// selection-extraction strategy. Populated by <see cref="IForegroundAppProbe"/>.
/// </summary>
public sealed record ForegroundAppInfo(string ProcessName, string? WindowTitle = null);
