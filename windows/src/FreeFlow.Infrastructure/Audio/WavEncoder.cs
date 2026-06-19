using System.Buffers.Binary;
using FreeFlow.Core.Audio;

namespace FreeFlow.Infrastructure.Audio;

/// <summary>
/// Encodes a PCM <see cref="AudioClip"/> into a canonical 44-byte-header WAV
/// container. Pure and deterministic (no clock, no I/O) so the exact bytes can
/// be asserted with a snapshot/contract oracle and fed to the transcription
/// provider as a multipart file part.
/// </summary>
public static class WavEncoder
{
    private const int HeaderSize = 44;
    private const short PcmFormat = 1;
    private const short BitsPerSample = 16;

    /// <summary>Build a complete WAV byte buffer for the clip.</summary>
    public static byte[] Encode(AudioClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return Encode(clip.Samples, clip.SampleRate, clip.Channels);
    }

    public static byte[] Encode(byte[] pcm, int sampleRate, int channels)
    {
        ArgumentNullException.ThrowIfNull(pcm);
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));

        var byteRate = sampleRate * channels * (BitsPerSample / 8);
        var blockAlign = (short)(channels * (BitsPerSample / 8));
        var dataSize = pcm.Length;
        var buffer = new byte[HeaderSize + dataSize];
        var span = buffer.AsSpan();

        // RIFF chunk descriptor.
        WriteAscii(span, 0, "RIFF");
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4), (uint)(36 + dataSize));
        WriteAscii(span, 8, "WAVE");

        // "fmt " sub-chunk.
        WriteAscii(span, 12, "fmt ");
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16), 16); // PCM fmt chunk size
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(20), PcmFormat);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(22), (short)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(24), (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(28), (uint)byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(32), blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span.Slice(34), BitsPerSample);

        // "data" sub-chunk.
        WriteAscii(span, 36, "data");
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(40), (uint)dataSize);
        pcm.CopyTo(span.Slice(HeaderSize));

        return buffer;
    }

    private static void WriteAscii(Span<byte> span, int offset, string ascii)
    {
        for (var i = 0; i < ascii.Length; i++)
        {
            span[offset + i] = (byte)ascii[i];
        }
    }
}
