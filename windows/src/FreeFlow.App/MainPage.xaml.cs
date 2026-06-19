using System;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FreeFlow_App;

/// <summary>
/// The main content page displayed inside the application window. Resolves the
/// dictation pipeline from DI and reflects its state live, whether a run is
/// started from the on-screen button or from a <b>global shortcut</b>
/// (hold-to-talk / toggle). Subscribing to the pipeline's
/// <see cref="DictationPipeline.StateChanged"/> is what makes the global
/// shortcuts visibly do something — previously the UI only updated from its own
/// button, so a hotkey-driven dictation produced no on-screen feedback.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly DictationPipeline _pipeline;

    public MainPage()
    {
        InitializeComponent();
        _pipeline = App.Services.GetRequiredService<DictationPipeline>();

        // Reflect runs triggered by the global keyboard shortcuts, not just the
        // button. Pipeline transitions can complete on a thread-pool thread, so
        // every UI update is marshaled back to the dispatcher.
        _pipeline.StateChanged += OnPipelineStateChanged;
        Unloaded += (_, _) => _pipeline.StateChanged -= OnPipelineStateChanged;

        ReflectState(_pipeline.State);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
        => Frame.Navigate(typeof(SettingsPage));

    private void OnRunLogClick(object sender, RoutedEventArgs e)
        => Frame.Navigate(typeof(RunLogPage));

    private async void OnPasteAgainClick(object sender, RoutedEventArgs e)
    {
        try
        {
            PasteAgainButton.IsEnabled = false;
            await _pipeline.PasteAgainAsync();
            if (_pipeline.LastRun is { } run)
            {
                ShowStatus(
                    InfoBarSeverity.Success,
                    "Pasted again",
                    $"Re-pasted the last cleaned text into {DescribeContext(run)}.");
            }
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, "Paste failed", ex.Message);
        }
        finally
        {
            PasteAgainButton.IsEnabled = _pipeline.LastRun is { } r
                && !string.IsNullOrEmpty(r.PostProcessedTranscript);
        }
    }

    private async void OnRunDictationClick(object sender, RoutedEventArgs e)
    {
        // Toggle: first click starts recording, second click stops & processes.
        // Result rendering and status come from the shared state handler below,
        // so the button and the global shortcut behave identically.
        try
        {
            if (_pipeline.State != DictationState.Recording)
            {
                await _pipeline.StartRecordingAsync(new DictationRequest
                {
                    Settings = new DictationSettings { PostProcessingEnabled = true },
                });
            }
            else
            {
                RunButton.IsEnabled = false;
                try
                {
                    await _pipeline.StopAndProcessAsync();
                }
                finally
                {
                    RunButton.IsEnabled = true;
                }
            }
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, "Pipeline error", ex.Message);
            RunLabel.Text = "Record & dictate";
        }
    }

    private void OnPipelineStateChanged(DictationState from, DictationState to)
    {
        var dispatcher = DispatcherQueue;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.TryEnqueue(() => ReflectState(to));
    }

    private void ReflectState(DictationState state)
    {
        switch (state)
        {
            case DictationState.Arming:
            case DictationState.Recording:
                RunLabel.Text = "Stop & paste";
                ShowStatus(
                    InfoBarSeverity.Informational,
                    "Listening…",
                    "Recording — release your hold-to-talk key, press your toggle, or click Stop & paste to finish.");
                break;

            case DictationState.Stopping:
            case DictationState.Transcribing:
            case DictationState.PostProcessing:
            case DictationState.Pasting:
                RunLabel.Text = "Stop & paste";
                ShowStatus(InfoBarSeverity.Informational, "Processing", "Transcribing and cleaning up…");
                break;

            case DictationState.Completed:
                if (_pipeline.LastRun is { } run)
                {
                    ReflectRun(run);
                }

                break;

            case DictationState.Error:
                RunLabel.Text = "Record & dictate";
                ShowStatus(
                    InfoBarSeverity.Error,
                    "Pipeline error",
                    "The dictation run failed. See the log at %LOCALAPPDATA%\\FreeFlow\\logs.");
                break;

            case DictationState.Cancelled:
            case DictationState.Idle:
                RunLabel.Text = "Record & dictate";
                break;
        }
    }

    private void ReflectRun(PipelineRun run)
    {
        RunLabel.Text = "Record & dictate";
        PasteAgainButton.IsEnabled = !string.IsNullOrEmpty(run.PostProcessedTranscript);
        ContextText.Text = DescribeContext(run);
        RawText.Text = string.IsNullOrEmpty(run.RawTranscript) ? "(empty)" : run.RawTranscript;
        CleanedText.Text = string.IsNullOrEmpty(run.PostProcessedTranscript)
            ? "(empty)"
            : run.PostProcessedTranscript;

        if (run.Pasted)
        {
            ShowStatus(
                InfoBarSeverity.Success,
                "Done",
                $"Pasted cleaned text into {DescribeContext(run)} (status: {run.PostProcessingStatus}).");
        }
        else
        {
            ShowStatus(
                InfoBarSeverity.Warning,
                "Nothing pasted",
                $"No text was produced (pipeline status: {run.PostProcessingStatus}).");
        }
    }

    private static string DescribeContext(PipelineRun run)
    {
        var app = run.Context.AppName ?? run.Context.ProcessName ?? "unknown";
        var title = run.Context.WindowTitle;
        return string.IsNullOrWhiteSpace(title) ? app : $"{app} — {title}";
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
