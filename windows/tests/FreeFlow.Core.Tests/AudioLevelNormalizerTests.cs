using FluentAssertions;
using FreeFlow.Core.Audio;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class AudioLevelNormalizerTests
{
    [Fact]
    public void Silence_reads_as_zero_after_settling()
    {
        var n = new AudioLevelNormalizer();

        var level = 0f;
        for (var i = 0; i < 50; i++)
        {
            level = n.NormalizedLevel(0f);
        }

        level.Should().Be(0f);
    }

    [Fact]
    public void Loud_speech_drives_meter_above_active_threshold()
    {
        var n = new AudioLevelNormalizer();

        // Establish a quiet noise floor, then present a sustained loud signal.
        for (var i = 0; i < 40; i++)
        {
            n.NormalizedLevel(0.0005f);
        }

        var level = 0f;
        for (var i = 0; i < 40; i++)
        {
            level = n.NormalizedLevel(0.4f);
        }

        level.Should().BeGreaterThan(0.12f);
    }

    [Fact]
    public void Output_is_always_clamped_to_unit_range()
    {
        var n = new AudioLevelNormalizer();

        foreach (var rms in new[] { 0f, 0.001f, 0.05f, 0.3f, 1.0f, 5.0f })
        {
            for (var i = 0; i < 10; i++)
            {
                var level = n.NormalizedLevel(rms);
                level.Should().BeInRange(0f, 1f);
            }
        }
    }

    [Fact]
    public void Reset_returns_meter_to_zero()
    {
        var n = new AudioLevelNormalizer();
        for (var i = 0; i < 20; i++)
        {
            n.NormalizedLevel(0.5f);
        }

        n.Reset();

        n.NormalizedLevel(0f).Should().Be(0f);
    }

    [Fact]
    public void Rms_of_empty_block_is_zero()
        => AudioLevelNormalizer.RmsFromPcm16(ReadOnlySpan<byte>.Empty).Should().Be(0f);

    [Fact]
    public void Rms_of_full_scale_square_wave_is_near_one()
    {
        // Alternating +full / -full scale samples => RMS ~= 1.0.
        var pcm = new byte[400];
        for (var i = 0; i < pcm.Length; i += 2)
        {
            short sample = (short)((i / 2) % 2 == 0 ? short.MaxValue : short.MinValue);
            pcm[i] = (byte)(sample & 0xFF);
            pcm[i + 1] = (byte)((sample >> 8) & 0xFF);
        }

        AudioLevelNormalizer.RmsFromPcm16(pcm).Should().BeGreaterThan(0.99f);
    }

    [Fact]
    public void Is_deterministic_for_identical_input_sequences()
    {
        var a = new AudioLevelNormalizer();
        var b = new AudioLevelNormalizer();
        var rng = new Random(42);

        for (var i = 0; i < 100; i++)
        {
            var rms = (float)rng.NextDouble();
            a.NormalizedLevel(rms).Should().Be(b.NormalizedLevel(rms));
        }
    }
}
