using FluentAssertions;
using FreeFlow.Core.Audio;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class AudioClipTests
{
    [Fact]
    public void Empty_clip_reports_empty_and_zero_duration()
    {
        AudioClip.Empty.IsEmpty.Should().BeTrue();
        AudioClip.Empty.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Duration_is_computed_from_pcm16_mono_16k()
    {
        // 16 kHz mono, 2 bytes/sample => 32000 bytes == 1 second.
        var clip = new AudioClip(new byte[32000]);

        clip.Duration.Should().Be(TimeSpan.FromSeconds(1));
        clip.IsEmpty.Should().BeFalse();
    }
}
