using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using FreeFlow.Core.Audio;
using FreeFlow.Infrastructure.Audio;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L1")]
public class WavEncoderTests
{
    [Fact]
    public void Encodes_canonical_44_byte_header()
    {
        var pcm = new byte[3200]; // 100 ms of silence at 16 kHz mono PCM16
        var bytes = WavEncoder.Encode(new AudioClip(pcm, SampleRate: 16000, Channels: 1));

        bytes.Length.Should().Be(44 + pcm.Length);

        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("RIFF");
        Encoding.ASCII.GetString(bytes, 8, 4).Should().Be("WAVE");
        Encoding.ASCII.GetString(bytes, 12, 4).Should().Be("fmt ");
        Encoding.ASCII.GetString(bytes, 36, 4).Should().Be("data");

        var span = bytes.AsSpan();
        BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4)).Should().Be((uint)(36 + pcm.Length));
        BinaryPrimitives.ReadInt16LittleEndian(span.Slice(20)).Should().Be(1); // PCM
        BinaryPrimitives.ReadInt16LittleEndian(span.Slice(22)).Should().Be(1); // mono
        BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(24)).Should().Be(16000);
        BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(28)).Should().Be(32000); // byte rate
        BinaryPrimitives.ReadInt16LittleEndian(span.Slice(32)).Should().Be(2); // block align
        BinaryPrimitives.ReadInt16LittleEndian(span.Slice(34)).Should().Be(16); // bits
        BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(40)).Should().Be((uint)pcm.Length);
    }

    [Fact]
    public void Is_deterministic_for_identical_input()
    {
        var clip = new AudioClip(new byte[] { 1, 2, 3, 4, 5, 6 }, 16000, 1);
        WavEncoder.Encode(clip).Should().Equal(WavEncoder.Encode(clip));
    }

    [Fact]
    public void Copies_pcm_payload_after_header()
    {
        var pcm = new byte[] { 9, 8, 7, 6 };
        var bytes = WavEncoder.Encode(new AudioClip(pcm, 16000, 1));
        bytes.AsSpan(44).ToArray().Should().Equal(pcm);
    }
}
