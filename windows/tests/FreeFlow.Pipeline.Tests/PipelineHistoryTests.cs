using FluentAssertions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;
using FreeFlow.TestKit;
using Microsoft.Extensions.Time.Testing;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L3")]
public class PipelineHistoryTests
{
    private static (DictationPipeline Pipeline, InMemoryHistoryStore History) Build()
    {
        var history = new InMemoryHistoryStore();
        var pipeline = new DictationPipeline(
            new FixtureAudioCapture(new AudioClip(new byte[3200])),
            new CassetteTranscriptionClient("um hello world"),
            new ScriptedPostProcessingClient("Hello world."),
            new StaticContextService(new CaptureContext("Notepad", "notepad", "Untitled - Notepad", null)),
            new InMemoryClipboardPasteService(),
            new SeededIdProvider(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-19T00:00:00Z")),
            logger: null,
            history: history);
        return (pipeline, history);
    }

    [Fact]
    public async Task Completed_run_is_persisted_to_history()
    {
        var (pipeline, history) = Build();

        await pipeline.StartRecordingAsync(new DictationRequest
        {
            Settings = new DictationSettings { PostProcessingEnabled = true },
        });
        var run = await pipeline.StopAndProcessAsync();

        var entries = await history.LoadAsync();
        entries.Should().ContainSingle();
        var entry = entries[0];
        entry.Id.Should().Be(run.Id);
        entry.RawTranscript.Should().Be("um hello world");
        entry.PostProcessedTranscript.Should().Be("Hello world.");
        entry.AppName.Should().Be("Notepad");
        entry.WindowTitle.Should().Be("Untitled - Notepad");
        entry.Pasted.Should().BeTrue();
        entry.Intent.Should().Be("dictation");
    }

    [Fact]
    public async Task History_persistence_is_optional()
    {
        var pipeline = new DictationPipeline(
            new FixtureAudioCapture(new AudioClip(new byte[3200])),
            new CassetteTranscriptionClient("hello"),
            new ScriptedPostProcessingClient("Hello."),
            new StaticContextService(CaptureContext.None),
            new InMemoryClipboardPasteService(),
            new SeededIdProvider(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-19T00:00:00Z")));

        await pipeline.StartRecordingAsync(new DictationRequest());
        var run = await pipeline.StopAndProcessAsync();

        run.PostProcessedTranscript.Should().Be("Hello.");
    }
}
