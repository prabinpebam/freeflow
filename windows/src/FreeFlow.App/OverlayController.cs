using System;
using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Pipeline;
using Microsoft.UI.Dispatching;

namespace FreeFlow_App;

/// <summary>
/// Drives the <see cref="OverlayWindow"/> from the dictation pipeline's state and
/// the live microphone level. Created on the UI thread; all window updates are
/// marshaled back onto that thread's <see cref="DispatcherQueue"/> because
/// pipeline transitions and audio callbacks arrive on background threads.
/// </summary>
public sealed class OverlayController : IDisposable
{
    private readonly DictationPipeline _pipeline;
    private readonly IAudioLevelMonitor? _levels;
    private readonly DispatcherQueue _dispatcher;
    private readonly Func<bool> _enabled;

    private OverlayWindow? _overlay;
    private bool _recording;

    public OverlayController(
        DictationPipeline pipeline,
        IAudioLevelMonitor? levels,
        DispatcherQueue dispatcher,
        Func<bool> enabled)
    {
        _pipeline = pipeline;
        _levels = levels;
        _dispatcher = dispatcher;
        _enabled = enabled;

        _pipeline.StateChanged += OnStateChanged;
        if (_levels is not null)
        {
            _levels.LevelChanged += OnLevelChanged;
        }
    }

    private void OnStateChanged(DictationState from, DictationState to)
        => _dispatcher.TryEnqueue(() => Apply(to));

    private void Apply(DictationState state)
    {
        if (!_enabled())
        {
            _recording = false;
            _overlay?.HideOverlay();
            return;
        }

        switch (state)
        {
            case DictationState.Arming:
            case DictationState.Recording:
                _recording = true;
                EnsureOverlay().ShowRecording();
                break;

            case DictationState.Stopping:
            case DictationState.Transcribing:
            case DictationState.PostProcessing:
            case DictationState.Pasting:
                _recording = false;
                EnsureOverlay().ShowTranscribing();
                break;

            default:
                _recording = false;
                _overlay?.HideOverlay();
                break;
        }
    }

    private void OnLevelChanged(float level)
    {
        if (!_recording)
        {
            return;
        }

        _dispatcher.TryEnqueue(() =>
        {
            if (_recording)
            {
                _overlay?.UpdateLevel(level);
            }
        });
    }

    private OverlayWindow EnsureOverlay() => _overlay ??= new OverlayWindow();

    public void Dispose()
    {
        _pipeline.StateChanged -= OnStateChanged;
        if (_levels is not null)
        {
            _levels.LevelChanged -= OnLevelChanged;
        }
    }
}
