using System.Text;
using FreeFlow.Core.Context;
using Interop.UIAutomationClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Platform.Context;

/// <summary>
/// Side-effect-free selection reader backed by UI Automation: reads the focused
/// element's <c>TextPattern</c> selection directly, without synthesizing keystrokes
/// or touching the clipboard. Preferred over the Ctrl+C probe wherever the app
/// exposes a text pattern (richer, and safe in consoles). Returns null when the
/// focused element has no text pattern or no selection (see known-limitations
/// LIM-007). Real OS/COM I/O — exercised at L4; the inner loop uses fakes.
/// </summary>
public sealed class UiaSelectionReader : ISelectionReader
{
    private readonly ILogger<UiaSelectionReader> _logger;
    private readonly Lazy<IUIAutomation> _automation;

    public UiaSelectionReader(ILogger<UiaSelectionReader>? logger = null)
    {
        _logger = logger ?? NullLogger<UiaSelectionReader>.Instance;
        _automation = new Lazy<IUIAutomation>(() => new CUIAutomation());
    }

    public Task<string?> TryReadSelectionAsync(CancellationToken ct = default)
        => Task.Run(() => ReadSelection(ct), ct);

    private string? ReadSelection(CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            var focused = _automation.Value.GetFocusedElement();
            if (focused is null)
            {
                return null;
            }

            if (focused.GetCurrentPattern(UIA_PatternIds.UIA_TextPatternId) is not IUIAutomationTextPattern textPattern)
            {
                return null;
            }

            var ranges = textPattern.GetSelection();
            if (ranges is null || ranges.Length == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < ranges.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var text = ranges.GetElement(i)?.GetText(-1);
                if (!string.IsNullOrEmpty(text))
                {
                    builder.Append(text);
                }
            }

            var selection = builder.ToString();
            return string.IsNullOrEmpty(selection) ? null : selection;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UI Automation selection read failed; will fall back.");
            return null;
        }
    }
}
