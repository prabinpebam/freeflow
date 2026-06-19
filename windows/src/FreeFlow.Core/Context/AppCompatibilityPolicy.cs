namespace FreeFlow.Core.Context;

/// <summary>
/// Pure policy deciding which selection-extraction strategies are safe for a given
/// foreground app, and in what order to try them (see known-limitations LIM-007).
///
/// Key safety rule: in apps where synthesizing <c>Ctrl+C</c> is destructive — most
/// notably terminals/consoles where Ctrl+C sends an interrupt — we must NEVER fall
/// back to the clipboard-copy probe. Such apps get UI Automation only; if that
/// yields nothing we report no selection rather than risk side effects.
///
/// UI Automation is always attempted first because it has no side effects.
/// </summary>
public static class AppCompatibilityPolicy
{
    // Process names (no extension, case-insensitive) where Ctrl+C is unsafe.
    private static readonly HashSet<string> ClipboardCopyUnsafe = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd",
        "conhost",
        "powershell",
        "pwsh",
        "windowsterminal",
        "wt",
        "mintty",
        "putty",
        "bash",
        "wsl",
        "wslhost",
    };

    public static bool IsClipboardCopyUnsafe(ForegroundAppInfo? app)
        => app is not null && ClipboardCopyUnsafe.Contains(NormalizeProcessName(app.ProcessName));

    /// <summary>
    /// Ordered, de-duplicated strategies to attempt for this app. UI Automation is
    /// always first; clipboard-copy is appended only when it is safe. A null app
    /// (unknown foreground) is treated conservatively as a normal app.
    /// </summary>
    public static IReadOnlyList<SelectionStrategy> StrategiesFor(ForegroundAppInfo? app)
    {
        var order = new List<SelectionStrategy> { SelectionStrategy.UiAutomation };
        if (!IsClipboardCopyUnsafe(app))
        {
            order.Add(SelectionStrategy.ClipboardCopy);
        }

        return order;
    }

    private static string NormalizeProcessName(string processName)
    {
        var name = processName.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        return name;
    }
}
