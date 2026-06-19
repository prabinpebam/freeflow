using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FreeFlow_App;

/// <summary>
/// The main content page displayed inside the application window. Resolves the
/// dictation pipeline from DI and runs a full mock round-trip (context capture →
/// record → transcribe → clean up → paste) so the wiring is visibly exercised.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly DictationPipeline _pipeline;

    public MainPage()
    {
        InitializeComponent();
        _pipeline = App.Services.GetRequiredService<DictationPipeline>();
    }

    private async void OnRunDictationClick(object sender, RoutedEventArgs e)
    {
        RunButton.IsEnabled = false;
        ShowStatus(InfoBarSeverity.Informational, "Running", "Capturing context and recording…");

        try
        {
            await _pipeline.StartRecordingAsync(new DictationRequest
            {
                Settings = new DictationSettings { PostProcessingEnabled = true },
            });

            var run = await _pipeline.StopAndProcessAsync();

            ContextText.Text = DescribeContext(run);
            RawText.Text = string.IsNullOrEmpty(run.RawTranscript) ? "(empty)" : run.RawTranscript;
            CleanedText.Text = string.IsNullOrEmpty(run.PostProcessedTranscript)
                ? "(empty)"
                : run.PostProcessedTranscript;

            if (run.Pasted)
            {
                ShowStatus(InfoBarSeverity.Success, "Done", $"Pasted via mock service (status: {run.PostProcessingStatus}).");
            }
            else
            {
                ShowStatus(InfoBarSeverity.Warning, "Nothing pasted", $"Pipeline status: {run.PostProcessingStatus}.");
            }
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, "Pipeline error", ex.Message);
        }
        finally
        {
            RunButton.IsEnabled = true;
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
