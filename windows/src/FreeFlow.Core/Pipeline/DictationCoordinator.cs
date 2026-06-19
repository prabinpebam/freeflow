using FreeFlow.Core.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Core.Pipeline;

/// <summary>
/// Connects global hotkey triggers to the dictation pipeline: hold-to-talk maps
/// to start/stop, toggle flips recording on each activation, and paste-again
/// repastes the last output. Kept free of any OS dependency (it talks to the
/// <see cref="IHotkeyService"/> seam) so the full input→pipeline wiring is
/// exercised deterministically with <c>FakeHotkeyService</c>.
/// </summary>
public sealed class DictationCoordinator : IDisposable
{
    private readonly IHotkeyService _hotkeys;
    private readonly DictationPipeline _pipeline;
    private readonly Func<DictationRequest> _requestFactory;
    private readonly ILogger<DictationCoordinator> _logger;

    private Task _current = Task.CompletedTask;
    private bool _recording;

    public DictationCoordinator(
        IHotkeyService hotkeys,
        DictationPipeline pipeline,
        Func<DictationRequest>? requestFactory = null,
        ILogger<DictationCoordinator>? logger = null)
    {
        _hotkeys = hotkeys;
        _pipeline = pipeline;
        _requestFactory = requestFactory ?? (() => new DictationRequest());
        _logger = logger ?? NullLogger<DictationCoordinator>.Instance;
        _hotkeys.Triggered += OnTriggered;
    }

    /// <summary>Whether a recording session is currently active.</summary>
    public bool IsRecording => _recording;

    /// <summary>The last started/continuing pipeline operation (for tests/shutdown).</summary>
    public Task Pending => _current;

    private void OnTriggered(HotkeyTrigger trigger)
    {
        switch (trigger)
        {
            case HotkeyTrigger.HoldStart:
                BeginRecording();
                break;
            case HotkeyTrigger.HoldStop:
                FinishRecording();
                break;
            case HotkeyTrigger.Toggle:
                if (_recording) FinishRecording();
                else BeginRecording();
                break;
            case HotkeyTrigger.PasteAgain:
                Chain(() => _pipeline.PasteAgainAsync());
                break;
        }
    }

    private void BeginRecording()
    {
        if (_recording)
        {
            return;
        }

        _recording = true;
        Chain(() => _pipeline.StartRecordingAsync(_requestFactory()));
    }

    private void FinishRecording()
    {
        if (!_recording)
        {
            return;
        }

        _recording = false;
        Chain(async () => { await _pipeline.StopAndProcessAsync().ConfigureAwait(false); });
    }

    /// <summary>Serialize pipeline operations so triggers can't interleave.</summary>
    private void Chain(Func<Task> operation)
    {
        _current = RunChained(operation);
    }

    private async Task RunChained(Func<Task> operation)
    {
        try
        {
            await _current.ConfigureAwait(false);
        }
        catch
        {
            // Prior failures are surfaced via their own task; don't block the next op.
        }

        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Coordinated pipeline operation failed.");
            _recording = false;
            throw;
        }
    }

    public void Dispose() => _hotkeys.Triggered -= OnTriggered;
}
