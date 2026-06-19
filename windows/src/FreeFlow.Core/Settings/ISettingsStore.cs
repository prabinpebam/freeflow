namespace FreeFlow.Core.Settings;

/// <summary>
/// Loads and persists the full <see cref="AppSettings"/> aggregate. Implementations
/// must be corruption-resilient: a missing or damaged store loads as
/// <see cref="AppSettings.Defaults"/> rather than throwing, so the app always
/// starts. Sensitive fields are protected at rest via <see cref="ISecretProtector"/>.
/// </summary>
public interface ISettingsStore
{
    /// <summary>Load persisted settings, or defaults when none exist / are unreadable.</summary>
    AppSettings Load();

    /// <summary>Persist the given settings, protecting secrets at rest.</summary>
    void Save(AppSettings settings);
}
