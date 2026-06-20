using FreeFlow.Core.Context;
using FreeFlow.Core.Settings;

namespace FreeFlow.Core.Pipeline;

/// <summary>Canonical pipeline states (porting-plan Section 4.4).</summary>
public enum DictationState
{
    Idle,
    Arming,
    Recording,
    Stopping,
    Transcribing,
    PostProcessing,
    Pasting,
    Completed,
    Error,
    Cancelled,
}

/// <summary>Run intent; wire values match the macOS exporter format.</summary>
public enum DictationIntent
{
    Dictation,
    CommandAutomatic,
    CommandManual,
}

public static class DictationIntentExtensions
{
    public static string ToWireValue(this DictationIntent intent) => intent switch
    {
        DictationIntent.CommandAutomatic => "command:automatic",
        DictationIntent.CommandManual => "command:manual",
        _ => "dictation",
    };

    public static DictationIntent ParseIntent(string? value) => value switch
    {
        "command:automatic" => DictationIntent.CommandAutomatic,
        "command:manual" => DictationIntent.CommandManual,
        _ => DictationIntent.Dictation,
    };
}

public sealed class InvalidStateTransitionException : InvalidOperationException
{
    public DictationState From { get; }
    public DictationState To { get; }

    public InvalidStateTransitionException(DictationState from, DictationState to)
        : base($"Invalid dictation state transition: {from} -> {to}.")
    {
        From = from;
        To = to;
    }
}

public sealed record DictationRequest
{
    public DictationIntent Intent { get; init; } = DictationIntent.Dictation;
    public DictationSettings Settings { get; init; } = new();

    /// <summary>
    /// Whether the Edit Mode manual modifier was held when this run was triggered.
    /// Captured at trigger time and used (with the live selection) to resolve the
    /// effective intent when <see cref="Settings"/> enables manual Edit Mode.
    /// </summary>
    public bool ManualModifierHeld { get; init; }
}

public sealed record TranscriptionOptions(string CustomVocabulary);

public sealed record PostProcessingRequest(
    string RawTranscript,
    DictationSettings Settings,
    CaptureContext Context,
    DictationIntent Intent = DictationIntent.Dictation);

/// <summary>
/// Recorded result of a single pipeline run. The Windows analog of the macOS
/// <c>PipelineHistoryItem</c>, kept compatible with the golden-corpus schema.
/// </summary>
public sealed record PipelineRun
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required DictationIntent Intent { get; init; }
    public required string RawTranscript { get; init; }
    public required string PostProcessedTranscript { get; init; }
    public string ContextSummary { get; init; } = string.Empty;
    public string PostProcessingStatus { get; init; } = string.Empty;
    public CaptureContext Context { get; init; } = CaptureContext.None;
    public bool Pasted { get; init; }
}
