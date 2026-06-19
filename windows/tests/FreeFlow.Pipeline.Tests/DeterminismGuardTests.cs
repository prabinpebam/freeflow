using FluentAssertions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;
using FreeFlow.TestKit;
using Microsoft.Extensions.Time.Testing;

namespace FreeFlow.Pipeline.Tests;

/// <summary>
/// Determinism guard (testing-strategy.md, Section 5): re-runs the L3 dictation
/// path repeatedly with identical seeded inputs and asserts byte-identical
/// verdicts every iteration. If this trips, build-verdict.ps1 sets
/// <c>determinism_ok = false</c> (it keys off the word "determinism" in the
/// failing test id) and the agentic loop goes red even when nothing else fails.
/// </summary>
[Trait("Tier", "L3")]
public class DeterminismGuardTests
{
    private static readonly (string Raw, string Cleaned)[] Cases =
    {
        ("um hello world", "Hello world."),
        ("send the report by uh friday", "Send the report by Friday."),
        ("   ", string.Empty),
        ("meeting at three pm tomorrow", "Meeting at 3 PM tomorrow."),
    };

    private static DictationPipeline BuildPipeline(string raw, string cleaned)
        => new(
            new FixtureAudioCapture(new AudioClip(new byte[3200])),
            new CassetteTranscriptionClient(raw),
            new ScriptedPostProcessingClient(cleaned),
            new StaticContextService(new CaptureContext("Notepad", "notepad", "Untitled - Notepad", null)),
            new InMemoryClipboardPasteService(),
            new SeededIdProvider(seed: 42),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-19T00:00:00Z")));

    private static async Task<string> RunVerdictAsync(string raw, string cleaned)
    {
        var pipeline = BuildPipeline(raw, cleaned);
        await pipeline.StartRecordingAsync(
            new DictationRequest { Settings = new DictationSettings { PostProcessingEnabled = true } });
        var run = await pipeline.StopAndProcessAsync();

        // Project the run into a stable string covering every observable field.
        return string.Join(
            '|',
            run.Id,
            run.Timestamp.ToString("O"),
            run.Intent,
            run.RawTranscript,
            run.PostProcessedTranscript,
            run.PostProcessingStatus,
            run.Pasted);
    }

    [Fact]
    public async Task L3_dictation_path_is_byte_identical_across_repeated_runs()
    {
        const int iterations = 5;

        foreach (var (raw, cleaned) in Cases)
        {
            var baseline = await RunVerdictAsync(raw, cleaned);

            for (var i = 1; i < iterations; i++)
            {
                var repeat = await RunVerdictAsync(raw, cleaned);
                repeat.Should().Be(
                    baseline,
                    "the seeded L3 pipeline must be deterministic for input '{0}' (iteration {1})",
                    raw,
                    i);
            }
        }
    }

    [Fact]
    public void Seeded_id_provider_is_reproducible()
    {
        var a = new SeededIdProvider(seed: 7);
        var b = new SeededIdProvider(seed: 7);

        var first = new[] { a.NewId(), a.NewId(), a.NewId() };
        var second = new[] { b.NewId(), b.NewId(), b.NewId() };

        first.Should().Equal(second);
    }
}
