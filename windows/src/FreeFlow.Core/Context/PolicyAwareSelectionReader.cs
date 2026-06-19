using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Core.Context;

/// <summary>
/// An <see cref="ISelectionReader"/> that consults <see cref="AppCompatibilityPolicy"/>
/// to choose, per foreground app, which underlying readers to try and in what order.
/// It probes the foreground app, asks the policy for an ordered strategy list, then
/// invokes the mapped readers until one returns non-empty text. Terminals never reach
/// the clipboard-copy reader (the policy omits it), so we never fire Ctrl+C into a
/// console.
///
/// This orchestration is pure (the OS work is behind the injected readers/probe), so
/// the inner loop asserts the routing with fakes (L1). Wire UIA + clipboard adapters
/// behind it in the real composition.
/// </summary>
public sealed class PolicyAwareSelectionReader : ISelectionReader
{
    private readonly IForegroundAppProbe _probe;
    private readonly ISelectionReader? _uiAutomation;
    private readonly ISelectionReader? _clipboardCopy;
    private readonly ILogger<PolicyAwareSelectionReader> _logger;

    public PolicyAwareSelectionReader(
        IForegroundAppProbe probe,
        ISelectionReader? uiAutomation,
        ISelectionReader? clipboardCopy,
        ILogger<PolicyAwareSelectionReader>? logger = null)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _uiAutomation = uiAutomation;
        _clipboardCopy = clipboardCopy;
        _logger = logger ?? NullLogger<PolicyAwareSelectionReader>.Instance;
    }

    public async Task<string?> TryReadSelectionAsync(CancellationToken ct = default)
    {
        var app = _probe.TryGetForegroundApp();
        var strategies = AppCompatibilityPolicy.StrategiesFor(app);

        foreach (var strategy in strategies)
        {
            var reader = Map(strategy);
            if (reader is null)
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();
            var selection = await reader.TryReadSelectionAsync(ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(selection))
            {
                _logger.LogDebug(
                    "Selection captured via {Strategy} for {Process}.",
                    strategy,
                    app?.ProcessName ?? "(unknown)");
                return selection;
            }
        }

        return null;
    }

    private ISelectionReader? Map(SelectionStrategy strategy) => strategy switch
    {
        SelectionStrategy.UiAutomation => _uiAutomation,
        SelectionStrategy.ClipboardCopy => _clipboardCopy,
        _ => null,
    };
}
