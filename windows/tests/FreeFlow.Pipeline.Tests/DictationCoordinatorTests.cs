using FluentAssertions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Input;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;
using FreeFlow.TestKit;
using Microsoft.Extensions.Time.Testing;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L3")]
public class DictationCoordinatorTests
{
    private static (FakeHotkeyService Hotkeys, DictationCoordinator Coordinator, InMemoryClipboardPasteService Clipboard)
        Build()
    {
        var clipboard = new InMemoryClipboardPasteService();
        var pipeline = new DictationPipeline(
            new FixtureAudioCapture(new AudioClip(new byte[3200])),
            new CassetteTranscriptionClient("um hello world"),
            new ScriptedPostProcessingClient("Hello world."),
            new StaticContextService(new CaptureContext("Notepad", "notepad", "Untitled - Notepad", null)),
            clipboard,
            new SeededIdProvider(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-19T00:00:00Z")));

        var hotkeys = new FakeHotkeyService();
        var coordinator = new DictationCoordinator(
            hotkeys,
            pipeline,
            () => new DictationRequest { Settings = new DictationSettings { PostProcessingEnabled = true } });

        return (hotkeys, coordinator, clipboard);
    }

    [Fact]
    public async Task Hold_start_then_stop_runs_full_pipeline_and_pastes()
    {
        var (hotkeys, coordinator, clipboard) = Build();

        hotkeys.Raise(HotkeyTrigger.HoldStart);
        await coordinator.Pending;
        coordinator.IsRecording.Should().BeTrue();

        hotkeys.Raise(HotkeyTrigger.HoldStop);
        await coordinator.Pending;

        coordinator.IsRecording.Should().BeFalse();
        clipboard.LastPasted.Should().Be("Hello world.");
    }

    [Fact]
    public async Task Toggle_flips_recording_on_each_activation()
    {
        var (hotkeys, coordinator, clipboard) = Build();

        hotkeys.Raise(HotkeyTrigger.Toggle);
        await coordinator.Pending;
        coordinator.IsRecording.Should().BeTrue();

        hotkeys.Raise(HotkeyTrigger.Toggle);
        await coordinator.Pending;
        coordinator.IsRecording.Should().BeFalse();
        clipboard.LastPasted.Should().Be("Hello world.");
    }

    [Fact]
    public async Task Paste_again_repastes_last_output()
    {
        var (hotkeys, coordinator, clipboard) = Build();

        hotkeys.Raise(HotkeyTrigger.HoldStart);
        await coordinator.Pending;
        hotkeys.Raise(HotkeyTrigger.HoldStop);
        await coordinator.Pending;

        clipboard.PasteLog.Clear();
        hotkeys.Raise(HotkeyTrigger.PasteAgain);
        await coordinator.Pending;

        clipboard.PasteLog.Should().ContainSingle().Which.Should().Be("Hello world.");
    }

    [Fact]
    public async Task Duplicate_hold_start_is_ignored()
    {
        var (hotkeys, coordinator, _) = Build();

        hotkeys.Raise(HotkeyTrigger.HoldStart);
        await coordinator.Pending;
        hotkeys.Raise(HotkeyTrigger.HoldStart);
        await coordinator.Pending;

        coordinator.IsRecording.Should().BeTrue();
    }
}
