using FluentAssertions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;
using FreeFlow.TestKit;
using Microsoft.Extensions.Time.Testing;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L3")]
public class CommandModePipelineTests
{
    private static (DictationPipeline Pipeline, ScriptedPostProcessingClient Post, InMemoryClipboardPasteService Clipboard)
        Build(string? selection, string transformed = "THE QUICK BROWN FOX", bool postProcessEnabled = false)
    {
        var clipboard = new InMemoryClipboardPasteService();
        var post = new ScriptedPostProcessingClient(transformed);
        var pipeline = new DictationPipeline(
            new FixtureAudioCapture(new AudioClip(new byte[3200])),
            new CassetteTranscriptionClient("make it uppercase"),
            post,
            new StaticContextService(new CaptureContext("Word", "winword", "Doc - Word", null)),
            clipboard,
            new SeededIdProvider(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-19T00:00:00Z")),
            selection: new FakeSelectionReader(selection));
        return (pipeline, post, clipboard);
    }

    private static DictationRequest CommandRequest(bool postProcessEnabled = false)
        => new()
        {
            Intent = DictationIntent.CommandManual,
            Settings = new DictationSettings { PostProcessingEnabled = postProcessEnabled },
        };

    [Fact]
    public async Task Command_mode_captures_selection_and_pastes_transformed_text()
    {
        var (pipeline, post, clipboard) = Build(selection: "the quick brown fox");

        await pipeline.StartRecordingAsync(CommandRequest());
        var run = await pipeline.StopAndProcessAsync();

        post.LastRequest!.Intent.Should().Be(DictationIntent.CommandManual);
        post.LastRequest!.Context.SelectedText.Should().Be("the quick brown fox");
        run.PostProcessedTranscript.Should().Be("THE QUICK BROWN FOX");
        clipboard.LastPasted.Should().Be("THE QUICK BROWN FOX");
        run.PostProcessingStatus.Should().Be("ok");
    }

    [Fact]
    public async Task Command_mode_runs_even_when_post_processing_toggle_is_off()
    {
        // Cleanup is globally disabled, but command mode must still invoke the model.
        var (pipeline, post, _) = Build(selection: "hello", postProcessEnabled: false);

        await pipeline.StartRecordingAsync(CommandRequest(postProcessEnabled: false));
        await pipeline.StopAndProcessAsync();

        post.LastRequest.Should().NotBeNull();
    }

    [Fact]
    public async Task Command_mode_without_selection_still_completes()
    {
        var (pipeline, post, _) = Build(selection: null);

        await pipeline.StartRecordingAsync(CommandRequest());
        var run = await pipeline.StopAndProcessAsync();

        post.LastRequest!.Context.HasSelection.Should().BeFalse();
        run.PostProcessingStatus.Should().Be("ok");
    }
}
