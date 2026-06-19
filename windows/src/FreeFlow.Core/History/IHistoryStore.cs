using FreeFlow.Core.Pipeline;

namespace FreeFlow.Core.History;

/// <summary>
/// A persisted record of one dictation run — the Windows analog of the macOS
/// <c>PipelineHistoryItem</c>. Kept flat and serialization-friendly.
/// </summary>
public sealed record HistoryEntry
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string Intent { get; init; }
    public required string RawTranscript { get; init; }
    public required string PostProcessedTranscript { get; init; }
    public string ContextSummary { get; init; } = string.Empty;
    public string PostProcessingStatus { get; init; } = string.Empty;
    public string? AppName { get; init; }
    public string? WindowTitle { get; init; }
    public bool Pasted { get; init; }

    public static HistoryEntry FromRun(PipelineRun run) => new()
    {
        Id = run.Id,
        Timestamp = run.Timestamp,
        Intent = run.Intent.ToWireValue(),
        RawTranscript = run.RawTranscript,
        PostProcessedTranscript = run.PostProcessedTranscript,
        ContextSummary = run.ContextSummary,
        PostProcessingStatus = run.PostProcessingStatus,
        AppName = run.Context.AppName,
        WindowTitle = run.Context.WindowTitle,
        Pasted = run.Pasted,
    };
}

/// <summary>
/// Run-history persistence seam. The real adapter writes JSON under the user's
/// app-data folder; the inner-loop fake keeps entries in memory. Implementations
/// must be corruption-resilient: a damaged store loads as empty, never throws on
/// startup (porting-plan Section 8.1).
/// </summary>
public interface IHistoryStore
{
    /// <summary>Append an entry, enforcing the retention cap (newest-first).</summary>
    Task AddAsync(HistoryEntry entry, CancellationToken ct = default);

    /// <summary>Load all entries, newest first. Returns empty on a missing/corrupt store.</summary>
    Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken ct = default);

    /// <summary>Remove all entries.</summary>
    Task ClearAsync(CancellationToken ct = default);
}
