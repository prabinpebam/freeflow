namespace FreeFlow.Core.Context;

/// <summary>
/// Reads the user's currently selected text from the foreground application, used
/// by command/edit mode to know what to transform. The real adapter lives in the
/// Platform layer (UI Automation / clipboard copy) and is exercised at L4; the
/// inner loop uses a scripted fake. Returns null when nothing is selected or the
/// foreground app does not expose its selection.
/// </summary>
public interface ISelectionReader
{
    Task<string?> TryReadSelectionAsync(CancellationToken ct = default);
}
