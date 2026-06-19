namespace FreeFlow.Core.Audio;

/// <summary>
/// Normalized captured audio handed to the transcription stage.
/// Defaults mirror the pipeline target format (PCM16, 16 kHz, mono).
/// </summary>
public sealed record AudioClip(byte[] Samples, int SampleRate = 16000, int Channels = 1)
{
    public static readonly AudioClip Empty = new(Array.Empty<byte>());

    public bool IsEmpty => Samples.Length == 0;

    /// <summary>Duration assuming 16-bit (2-byte) samples.</summary>
    public TimeSpan Duration =>
        SampleRate <= 0 || Channels <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(Samples.Length / 2.0 / Channels / SampleRate);
}
