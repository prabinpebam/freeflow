namespace FreeFlow.Core.Updates;

/// <summary>Release channels the updater can follow.</summary>
public enum ReleaseChannel
{
    /// <summary>Only stable (non-pre-release) versions.</summary>
    Stable,

    /// <summary>Stable plus pre-release builds (opt-in beta).</summary>
    Beta,
}

/// <summary>
/// One published release from the Velopack/GitHub feed. <see cref="DownloadUrl"/>
/// is opaque to the planner (handed to the Platform updater adapter).
/// </summary>
public sealed record ReleaseInfo(SemanticVersion Version, string DownloadUrl)
{
    public bool IsPreRelease => Version.IsPreRelease;
}

/// <summary>Outcome of an update check.</summary>
public sealed record UpdateDecision(bool UpdateAvailable, ReleaseInfo? Target)
{
    public static readonly UpdateDecision None = new(false, null);
}

/// <summary>
/// Pure decision logic for "is there a newer release I should offer?". Given the
/// running version, a channel, and the available releases, it selects the highest
/// applicable version strictly greater than the current one. Network fetch and the
/// actual download/apply live in the Platform Velopack adapter (L4); keeping the
/// decision pure lets the inner loop assert every edge (downgrades, equal version,
/// channel filtering, pre-release precedence).
/// </summary>
public static class UpdatePlanner
{
    public static UpdateDecision Plan(
        SemanticVersion current,
        IEnumerable<ReleaseInfo> available,
        ReleaseChannel channel = ReleaseChannel.Stable)
    {
        ReleaseInfo? best = null;

        foreach (var release in available)
        {
            // Stable channel ignores pre-release builds entirely.
            if (channel == ReleaseChannel.Stable && release.IsPreRelease)
            {
                continue;
            }

            if (release.Version.CompareTo(current) <= 0)
            {
                continue; // not newer than what we run
            }

            if (best is null || release.Version.CompareTo(best.Version) > 0)
            {
                best = release;
            }
        }

        return best is null ? UpdateDecision.None : new UpdateDecision(true, best);
    }
}
