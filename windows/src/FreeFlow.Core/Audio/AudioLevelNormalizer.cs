namespace FreeFlow.Core.Audio;

/// <summary>
/// Adaptive microphone-level normalizer ported from the macOS app
/// (<c>LiveAudioLevelNormalizer.swift</c>). Converts a raw RMS amplitude into a
/// smoothed 0..1 level suitable for driving a recording overlay's meter. Tracks
/// an adaptive noise floor and peak ceiling so quiet rooms and loud rooms both
/// produce a lively, well-scaled meter, and gates background noise so silence
/// reads as zero.
///
/// Pure and deterministic (no time/OS dependencies): feed it successive RMS
/// values and it returns the display level, carrying smoothing state between
/// calls. Exercised directly by the inner loop.
/// </summary>
public sealed class AudioLevelNormalizer
{
    private const float MinimumRms = 0.00001f;
    private const float MinSpanDb = 18f;
    private const float PeakHeadroomDb = 8f;
    private const float SpeechGateMarginDb = 3f;
    private const float MinimumVisibleActiveLevel = 0.12f;
    private const float NoiseGateNormalizedThreshold = 0.06f;
    private const float FloorRiseWindowDb = 4f;
    private const float FloorFallBlend = 0.12f;
    private const float FloorRiseBlend = 0.02f;
    private const float PeakAttackBlend = 0.55f;
    private const float PeakReleaseBlend = 0.04f;
    private const float DisplayAttackBlend = 0.45f;
    private const float DisplayReleaseBlend = 0.12f;

    private float _noiseFloorDb = -55f;
    private float _peakCeilingDb = -37f;
    private float _displayLevel;

    /// <summary>Clears smoothing state so the next session starts fresh.</summary>
    public void Reset()
    {
        _noiseFloorDb = -55f;
        _peakCeilingDb = -37f;
        _displayLevel = 0f;
    }

    /// <summary>
    /// Computes the RMS amplitude (0..1) of a block of 16-bit mono PCM samples.
    /// Returns 0 for an empty block.
    /// </summary>
    public static float RmsFromPcm16(ReadOnlySpan<byte> pcm)
    {
        var sampleCount = pcm.Length / 2;
        if (sampleCount == 0)
        {
            return 0f;
        }

        double sumSquares = 0;
        for (var i = 0; i + 1 < pcm.Length; i += 2)
        {
            var sample = (short)(pcm[i] | (pcm[i + 1] << 8));
            var normalized = sample / 32768.0;
            sumSquares += normalized * normalized;
        }

        return (float)Math.Sqrt(sumSquares / sampleCount);
    }

    /// <summary>
    /// Feeds the next RMS amplitude and returns the smoothed 0..1 display level.
    /// </summary>
    public float NormalizedLevel(float rms)
    {
        var levelDb = 20f * MathF.Log10(MathF.Max(rms, MinimumRms));

        UpdateNoiseFloor(levelDb);
        UpdatePeakCeiling(levelDb);

        var displayCeilingDb = _peakCeilingDb + PeakHeadroomDb;
        var dynamicSpan = MathF.Max(displayCeilingDb - _noiseFloorDb, MinSpanDb + PeakHeadroomDb);
        var normalized = Clamp((levelDb - _noiseFloorDb) / dynamicSpan);
        var isActiveSpeech = levelDb >= _noiseFloorDb + SpeechGateMarginDb;

        if (normalized < NoiseGateNormalizedThreshold && levelDb <= _noiseFloorDb + SpeechGateMarginDb)
        {
            normalized = 0f;
        }
        else if (isActiveSpeech)
        {
            normalized = MathF.Max(normalized, MinimumVisibleActiveLevel);
        }

        var blend = normalized > _displayLevel ? DisplayAttackBlend : DisplayReleaseBlend;
        _displayLevel = Mix(_displayLevel, normalized, blend);
        return _displayLevel;
    }

    private void UpdateNoiseFloor(float levelDb)
    {
        var ceilingLimitedLevel = MathF.Min(levelDb, _peakCeilingDb - MinSpanDb);

        if (ceilingLimitedLevel <= _noiseFloorDb)
        {
            _noiseFloorDb = Mix(_noiseFloorDb, ceilingLimitedLevel, FloorFallBlend);
        }
        else if (ceilingLimitedLevel <= _noiseFloorDb + FloorRiseWindowDb)
        {
            _noiseFloorDb = Mix(_noiseFloorDb, ceilingLimitedLevel, FloorRiseBlend);
        }
    }

    private void UpdatePeakCeiling(float levelDb)
    {
        var minimumCeiling = _noiseFloorDb + MinSpanDb;

        if (levelDb >= _peakCeilingDb)
        {
            _peakCeilingDb = Mix(_peakCeilingDb, levelDb, PeakAttackBlend);
        }
        else
        {
            _peakCeilingDb = Mix(_peakCeilingDb, MathF.Max(levelDb, minimumCeiling), PeakReleaseBlend);
        }

        _peakCeilingDb = MathF.Max(_peakCeilingDb, minimumCeiling);
    }

    private static float Mix(float current, float target, float blend)
        => current + (target - current) * blend;

    private static float Clamp(float value)
        => MathF.Min(MathF.Max(value, 0f), 1f);
}
