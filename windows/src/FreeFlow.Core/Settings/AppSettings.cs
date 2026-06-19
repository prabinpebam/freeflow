using FreeFlow.Core.Input;
using FreeFlow.Core.Providers;

namespace FreeFlow.Core.Settings;

/// <summary>
/// The complete, persisted user configuration: provider endpoints/keys, global
/// shortcuts, pipeline behaviour, and general preferences. This is the single
/// aggregate the settings store reads and writes, and the setup wizard builds up
/// step by step. All members are immutable records so updates use <c>with</c>.
/// </summary>
public sealed record AppSettings
{
    public ProviderConfiguration Providers { get; init; } = new();

    public HotkeyBindings Hotkeys { get; init; } = HotkeyBindings.Defaults;

    public DictationSettings Dictation { get; init; } = new();

    public GeneralSettings General { get; init; } = new();

    /// <summary>A fresh configuration with built-in defaults.</summary>
    public static AppSettings Defaults => new();
}
