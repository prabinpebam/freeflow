using FluentAssertions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;
using FreeFlow.TestKit;
using Microsoft.Extensions.Time.Testing;
using Reqnroll;

namespace FreeFlow.Pipeline.Tests.Steps;

[Binding]
public sealed class DictationSteps
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-06-19T00:00:00Z"));
    private readonly InMemoryClipboardPasteService _clipboard = new();

    private StaticContextService _context = new(CaptureContext.None);
    private FixtureAudioCapture _audio = new(new AudioClip(new byte[3200]));
    private CassetteTranscriptionClient _transcription = new(string.Empty);
    private ScriptedPostProcessingClient _postProcessing = new(string.Empty);
    private DictationPipeline _pipeline = null!;

    [Given("the focused app is \"(.*)\"")]
    public void GivenTheFocusedAppIs(string app)
        => _context = new StaticContextService(new CaptureContext(app, app.ToLowerInvariant(), $"{app} - window", null));

    [Given("the clipboard currently contains \"(.*)\"")]
    public void GivenTheClipboardCurrentlyContains(string text) => _clipboard.Clipboard = text;

    [Given("the microphone will capture fixture audio")]
    public void GivenTheMicrophoneWillCaptureFixtureAudio()
        => _audio = new FixtureAudioCapture(new AudioClip(new byte[3200]));

    [Given("the transcription provider will return \"(.*)\"")]
    public void GivenTheTranscriptionProviderWillReturn(string transcript)
        => _transcription = new CassetteTranscriptionClient(transcript);

    [Given("the post-processor will return \"(.*)\"")]
    public void GivenThePostProcessorWillReturn(string cleaned)
        => _postProcessing = new ScriptedPostProcessingClient(cleaned);

    [When("the user holds the dictation shortcut for (.*) ms")]
    public async Task WhenTheUserHoldsTheDictationShortcutFor(int milliseconds)
    {
        _pipeline = new DictationPipeline(
            _audio, _transcription, _postProcessing, _context, _clipboard, new SeededIdProvider(), _time);
        await _pipeline.StartRecordingAsync(new DictationRequest());
        _time.Advance(TimeSpan.FromMilliseconds(milliseconds));
    }

    [When("the user releases the dictation shortcut")]
    public async Task WhenTheUserReleasesTheDictationShortcut()
        => await _pipeline.StopAndProcessAsync();

    [Then("the pipeline ends in state \"(.*)\"")]
    public void ThenThePipelineEndsInState(string state)
        => _pipeline.State.ToString().Should().Be(state);

    [Then("the pasted text is \"(.*)\"")]
    public void ThenThePastedTextIs(string text)
        => _clipboard.LastPasted.Should().Be(text);

    [Then("the clipboard is restored to \"(.*)\"")]
    public void ThenTheClipboardIsRestoredTo(string text)
        => _clipboard.Clipboard.Should().Be(text);
}
