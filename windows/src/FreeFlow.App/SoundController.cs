using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Pipeline;

namespace FreeFlow_App;

/// <summary>
/// Plays recording lifecycle audio cues from the dictation pipeline's state,
/// mirroring the macOS alert sounds. Honors the General &gt; play sounds toggle.
/// Cues are non-blocking and can be raised from any thread, so no dispatcher
/// marshaling is required.
/// </summary>
public sealed class SoundController : IDisposable
{
    private readonly DictationPipeline _pipeline;
    private readonly ISoundService _sounds;
    private readonly Func<bool> _enabled;

    public SoundController(DictationPipeline pipeline, ISoundService sounds, Func<bool> enabled)
    {
        _pipeline = pipeline;
        _sounds = sounds;
        _enabled = enabled;
        _pipeline.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(DictationState from, DictationState to)
    {
        if (!_enabled())
        {
            return;
        }

        switch (to)
        {
            case DictationState.Recording when from == DictationState.Arming:
                _sounds.Play(AppSound.RecordingStarted);
                break;

            case DictationState.Transcribing:
                _sounds.Play(AppSound.RecordingStopped);
                break;

            case DictationState.Completed:
                _sounds.Play(AppSound.TranscriptReady);
                break;

            case DictationState.Error:
                _sounds.Play(AppSound.Error);
                break;
        }
    }

    public void Dispose() => _pipeline.StateChanged -= OnStateChanged;
}
