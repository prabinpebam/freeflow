using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Context;
using FreeFlow.Core.History;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Core.Pipeline;

/// <summary>
/// Orchestrates one dictation session across the platform seams. All
/// non-determinism (audio, providers, clipboard, clock, ids) is injected, so
/// the pipeline is fully exercisable in the deterministic test tiers.
/// </summary>
public sealed class DictationPipeline
{
    private readonly IAudioCaptureService _audio;
    private readonly ITranscriptionClient _transcription;
    private readonly IPostProcessingClient _postProcessing;
    private readonly IContextService _context;
    private readonly IClipboardPasteService _paste;
    private readonly IIdProvider _ids;
    private readonly TimeProvider _time;
    private readonly ILogger<DictationPipeline> _logger;
    private readonly IHistoryStore? _history;
    private readonly DictationStateMachine _machine = new();

    private DictationRequest _request = new();
    private CaptureContext _capturedContext = CaptureContext.None;

    public DictationPipeline(
        IAudioCaptureService audio,
        ITranscriptionClient transcription,
        IPostProcessingClient postProcessing,
        IContextService context,
        IClipboardPasteService paste,
        IIdProvider ids,
        TimeProvider? time = null,
        ILogger<DictationPipeline>? logger = null,
        IHistoryStore? history = null)
    {
        _audio = audio;
        _transcription = transcription;
        _postProcessing = postProcessing;
        _context = context;
        _paste = paste;
        _ids = ids;
        _time = time ?? TimeProvider.System;
        _logger = logger ?? NullLogger<DictationPipeline>.Instance;
        _history = history;
        _machine.Transitioned += (from, to) =>
            _logger.LogDebug("Dictation state {From} -> {To}", from, to);
    }

    public DictationState State => _machine.State;

    public PipelineRun? LastRun { get; private set; }

    public event Action<DictationState, DictationState>? StateChanged
    {
        add => _machine.Transitioned += value;
        remove => _machine.Transitioned -= value;
    }

    /// <summary>Hold pressed: capture context, begin recording.</summary>
    public async Task StartRecordingAsync(DictationRequest request, CancellationToken ct = default)
    {
        _request = request ?? new DictationRequest();
        _logger.LogInformation("Starting dictation (intent={Intent}).", _request.Intent);
        _machine.TransitionTo(DictationState.Arming);
        _capturedContext = await _context.CaptureAsync(ct).ConfigureAwait(false);
        _logger.LogDebug(
            "Captured context app={App} process={Process}.",
            _capturedContext.AppName,
            _capturedContext.ProcessName);
        await _audio.StartAsync(ct).ConfigureAwait(false);
        _machine.TransitionTo(DictationState.Recording);
    }

    /// <summary>Hold released: stop, transcribe, clean up, paste, record the run.</summary>
    public async Task<PipelineRun> StopAndProcessAsync(CancellationToken ct = default)
    {
        try
        {
            _machine.TransitionTo(DictationState.Stopping);
            var clip = await _audio.StopAndGetClipAsync(ct).ConfigureAwait(false);

            _machine.TransitionTo(DictationState.Transcribing);
            var raw = await _transcription
                .TranscribeAsync(clip, new TranscriptionOptions(_request.Settings.CustomVocabulary), ct)
                .ConfigureAwait(false) ?? string.Empty;
            _logger.LogDebug("Transcribed {Length} chars.", raw.Length);

            _machine.TransitionTo(DictationState.PostProcessing);
            string processed;
            string status;
            if (string.IsNullOrWhiteSpace(raw))
            {
                processed = string.Empty;
                status = "empty";
            }
            else if (_request.Settings.PostProcessingEnabled)
            {
                processed = await _postProcessing
                    .CleanupAsync(new PostProcessingRequest(raw, _request.Settings, _capturedContext), ct)
                    .ConfigureAwait(false) ?? string.Empty;
                status = "ok";
            }
            else
            {
                processed = raw;
                status = "skipped";
            }

            _machine.TransitionTo(DictationState.Pasting);
            var pasted = false;
            if (!string.IsNullOrEmpty(processed))
            {
                await _paste.PasteTextAsync(processed, ct).ConfigureAwait(false);
                pasted = true;
            }

            var run = new PipelineRun
            {
                Id = _ids.NewId(),
                Timestamp = _time.GetUtcNow(),
                Intent = _request.Intent,
                RawTranscript = raw,
                PostProcessedTranscript = processed,
                ContextSummary = string.Empty,
                PostProcessingStatus = status,
                Context = _capturedContext,
                Pasted = pasted,
            };
            LastRun = run;

            _machine.TransitionTo(DictationState.Completed);
            _machine.TransitionTo(DictationState.Idle);

            if (_history is not null)
            {
                try
                {
                    await _history.AddAsync(HistoryEntry.FromRun(run), ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // History persistence must never fail a completed dictation.
                    _logger.LogWarning(ex, "Failed to persist history entry for run {RunId}.", run.Id);
                }
            }

            _logger.LogInformation(
                "Dictation complete (run={RunId} status={Status} pasted={Pasted}).",
                run.Id,
                status,
                pasted);
            return run;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dictation pipeline failed.");
            if (_machine.IsActive)
            {
                _machine.TransitionTo(DictationState.Error);
                _machine.TransitionTo(DictationState.Idle);
            }

            throw;
        }
    }

    /// <summary>Re-paste the last successful output (paste-again shortcut).</summary>
    public async Task PasteAgainAsync(CancellationToken ct = default)
    {
        if (LastRun is null || string.IsNullOrEmpty(LastRun.PostProcessedTranscript))
        {
            return;
        }

        await _paste.PasteTextAsync(LastRun.PostProcessedTranscript, ct).ConfigureAwait(false);
    }

    /// <summary>Cancel the active session (from any non-terminal state).</summary>
    public Task CancelAsync(CancellationToken ct = default)
    {
        if (_machine.IsActive)
        {
            _machine.Cancel();
            _machine.TransitionTo(DictationState.Idle);
        }

        return Task.CompletedTask;
    }
}
