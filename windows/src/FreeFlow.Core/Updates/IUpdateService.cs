namespace FreeFlow.Core.Updates;

/// <summary>
/// Platform seam for the real updater (Velopack over GitHub Releases). The Core
/// <see cref="UpdatePlanner"/> decides <em>whether</em> to update; the Platform
/// adapter performs the network check, download, and apply/restart. Kept tiny so
/// it can be faked in the inner loop and exercised for real at L4/L5.
/// </summary>
public interface IUpdateService
{
    /// <summary>The version of the currently running app.</summary>
    SemanticVersion CurrentVersion { get; }

    /// <summary>Fetches the feed and decides if a newer applicable release exists.</summary>
    Task<UpdateDecision> CheckForUpdatesAsync(
        ReleaseChannel channel = ReleaseChannel.Stable,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads and applies the target release, restarting the app.</summary>
    Task ApplyUpdateAsync(ReleaseInfo target, CancellationToken cancellationToken = default);
}
