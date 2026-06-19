using FluentAssertions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;
using FreeFlow.TestKit;
using Microsoft.Extensions.Time.Testing;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L3")]
public class DictationPipelineTests
{
    private static (DictationPipeline Pipeline, InMemoryClipboardPasteService Clipboard) Build(
        string raw = "um hello world",
        string cleaned = "Hello world.")
    {
        var clipboard = new InMemoryClipboardPasteService();
        var pipeline = new DictationPipeline(
            new FixtureAudioCapture(new AudioClip(new byte[3200])),
            new CassetteTranscriptionClient(raw),
            new ScriptedPostProcessingClient(cleaned),
            new StaticContextService(new CaptureContext("Notepad", "notepad", "Untitled - Notepad", null)),
            clipboard,
            new SeededIdProvider(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-19T00:00:00Z")));
        return (pipeline, clipboard);
    }

    private static DictationRequest Request(bool postProcess = true)
        => new() { Settings = new DictationSettings { PostProcessingEnabled = postProcess } };

    [Fact]
    public async Task Hold_to_talk_pastes_cleaned_text_and_ends_idle()
    {
        var (pipeline, clipboard) = Build();

        await pipeline.StartRecordingAsync(Request());
        var run = await pipeline.StopAndProcessAsync();

        run.RawTranscript.Should().Be("um hello world");
        run.PostProcessedTranscript.Should().Be("Hello world.");
        run.Pasted.Should().BeTrue();
        run.PostProcessingStatus.Should().Be("ok");
        clipboard.LastPasted.Should().Be("Hello world.");
        pipeline.State.Should().Be(DictationState.Idle);
    }

    [Fact]
    public async Task Clipboard_is_restored_after_paste()
    {
        var (pipeline, clipboard) = Build();
        clipboard.Clipboard = "PREVIOUS";

        await pipeline.StartRecordingAsync(Request());
        await pipeline.StopAndProcessAsync();

        clipboard.Clipboard.Should().Be("PREVIOUS");
    }

    [Fact]
    public async Task Empty_transcript_is_not_pasted()
    {
        var (pipeline, clipboard) = Build(raw: "   ");

        await pipeline.StartRecordingAsync(Request());
        var run = await pipeline.StopAndProcessAsync();

        run.Pasted.Should().BeFalse();
        run.PostProcessingStatus.Should().Be("empty");
        clipboard.LastPasted.Should().BeNull();
    }

    [Fact]
    public async Task Paste_again_repastes_last_successful_output()
    {
        var (pipeline, clipboard) = Build();

        await pipeline.StartRecordingAsync(Request());
        await pipeline.StopAndProcessAsync();
        clipboard.PasteLog.Clear();
        await pipeline.PasteAgainAsync();

        clipboard.PasteLog.Should().ContainSingle().Which.Should().Be("Hello world.");
    }

    [Fact]
    public async Task Disabled_post_processing_pastes_raw_transcript()
    {
        var (pipeline, clipboard) = Build();

        await pipeline.StartRecordingAsync(Request(postProcess: false));
        var run = await pipeline.StopAndProcessAsync();

        run.PostProcessedTranscript.Should().Be("um hello world");
        run.PostProcessingStatus.Should().Be("skipped");
        clipboard.LastPasted.Should().Be("um hello world");
    }

    [Fact]
    public async Task Spoken_press_enter_strips_command_pastes_text_and_presses_enter()
    {
        var clipboard = new InMemoryClipboardPasteService();
        var keystrokes = new RecordingKeystrokeSender();
        var pipeline = new DictationPipeline(
            new FixtureAudioCapture(new AudioClip(new byte[3200])),
            new CassetteTranscriptionClient("send it press enter"),
            new ScriptedPostProcessingClient("Send it, press enter."),
            new StaticContextService(new CaptureContext("Notepad", "notepad", "Untitled - Notepad", null)),
            clipboard,
            new SeededIdProvider(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-19T00:00:00Z")),
            keystrokes: keystrokes);

        await pipeline.StartRecordingAsync(Request());
        var run = await pipeline.StopAndProcessAsync();

        clipboard.LastPasted.Should().Be("Send it");
        run.PostProcessedTranscript.Should().Be("Send it");
        keystrokes.EnterCount.Should().Be(1);
    }

    [Fact]
    public async Task Press_enter_is_not_triggered_when_disabled()
    {
        var clipboard = new InMemoryClipboardPasteService();
        var keystrokes = new RecordingKeystrokeSender();
        var pipeline = new DictationPipeline(
            new FixtureAudioCapture(new AudioClip(new byte[3200])),
            new CassetteTranscriptionClient("send it press enter"),
            new ScriptedPostProcessingClient("Send it, press enter."),
            new StaticContextService(new CaptureContext("Notepad", "notepad", "Untitled - Notepad", null)),
            clipboard,
            new SeededIdProvider(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-19T00:00:00Z")),
            keystrokes: keystrokes);

        await pipeline.StartRecordingAsync(new DictationRequest
        {
            Settings = new DictationSettings { PostProcessingEnabled = true, PressEnterEnabled = false },
        });
        await pipeline.StopAndProcessAsync();

        clipboard.LastPasted.Should().Be("Send it, press enter.");
        keystrokes.EnterCount.Should().Be(0);
    }
}
