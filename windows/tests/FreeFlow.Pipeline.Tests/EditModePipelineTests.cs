using FluentAssertions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;
using FreeFlow.TestKit;
using Microsoft.Extensions.Time.Testing;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L3")]
public class EditModePipelineTests
{
    private static (DictationPipeline Pipeline, ScriptedPostProcessingClient Post) Build(string? selection)
    {
        var post = new ScriptedPostProcessingClient("TRANSFORMED");
        var pipeline = new DictationPipeline(
            new FixtureAudioCapture(new AudioClip(new byte[3200])),
            new CassetteTranscriptionClient("make it uppercase"),
            post,
            new StaticContextService(new CaptureContext("Word", "winword", "Doc - Word", null)),
            new InMemoryClipboardPasteService(),
            new SeededIdProvider(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-19T00:00:00Z")),
            selection: new FakeSelectionReader(selection));
        return (pipeline, post);
    }

    private static DictationRequest Request(EditModeSettings editMode, bool manualModifierHeld = false)
        => new()
        {
            // Caller leaves intent as Dictation; Edit Mode resolves the real intent.
            Settings = new DictationSettings { PostProcessingEnabled = false, EditMode = editMode },
            ManualModifierHeld = manualModifierHeld,
        };

    [Fact]
    public async Task Automatic_edit_mode_with_selection_runs_as_command()
    {
        var (pipeline, post) = Build(selection: "the quick brown fox");

        await pipeline.StartRecordingAsync(Request(
            new EditModeSettings { Enabled = true, Style = CommandModeStyle.Automatic }));
        var run = await pipeline.StopAndProcessAsync();

        run.Intent.Should().Be(DictationIntent.CommandAutomatic);
        post.LastRequest!.Intent.Should().Be(DictationIntent.CommandAutomatic);
        post.LastRequest!.Context.SelectedText.Should().Be("the quick brown fox");
    }

    [Fact]
    public async Task Automatic_edit_mode_without_selection_runs_as_dictation()
    {
        var (pipeline, post) = Build(selection: null);

        await pipeline.StartRecordingAsync(Request(
            new EditModeSettings { Enabled = true, Style = CommandModeStyle.Automatic }));
        var run = await pipeline.StopAndProcessAsync();

        run.Intent.Should().Be(DictationIntent.Dictation);
        // Cleanup is off and this is plain dictation, so the model is never called.
        post.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task Manual_edit_mode_with_modifier_and_selection_runs_as_command()
    {
        var (pipeline, post) = Build(selection: "hello world");

        await pipeline.StartRecordingAsync(Request(
            new EditModeSettings { Enabled = true, Style = CommandModeStyle.Manual },
            manualModifierHeld: true));
        var run = await pipeline.StopAndProcessAsync();

        run.Intent.Should().Be(DictationIntent.CommandManual);
        post.LastRequest!.Intent.Should().Be(DictationIntent.CommandManual);
    }

    [Fact]
    public async Task Manual_edit_mode_without_modifier_runs_as_dictation()
    {
        var (pipeline, post) = Build(selection: "hello world");

        await pipeline.StartRecordingAsync(Request(
            new EditModeSettings { Enabled = true, Style = CommandModeStyle.Manual },
            manualModifierHeld: false));
        var run = await pipeline.StopAndProcessAsync();

        run.Intent.Should().Be(DictationIntent.Dictation);
        post.LastRequest.Should().BeNull();
    }
}
