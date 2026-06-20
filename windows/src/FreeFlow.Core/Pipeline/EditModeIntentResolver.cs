using FreeFlow.Core.Settings;

namespace FreeFlow.Core.Pipeline;

/// <summary>
/// Decides whether a dictation trigger should run as plain dictation or as an
/// Edit Mode (command) transform of the current selection. Pure logic mirroring
/// the macOS <c>resolveSessionIntent</c> so the decision is fully testable.
/// </summary>
public static class EditModeIntentResolver
{
    /// <summary>
    /// Resolve the run intent from the user's Edit Mode settings and the live
    /// trigger state.
    /// </summary>
    /// <param name="settings">Edit Mode preferences.</param>
    /// <param name="hasSelection">Whether non-empty text is selected in the target app.</param>
    /// <param name="manualModifierHeld">
    /// Whether the configured manual modifier was held at trigger time. Only
    /// consulted in <see cref="CommandModeStyle.Manual"/>.
    /// </param>
    public static DictationIntent Resolve(
        EditModeSettings settings,
        bool hasSelection,
        bool manualModifierHeld)
    {
        if (settings is null || !settings.Enabled)
        {
            return DictationIntent.Dictation;
        }

        return settings.Style switch
        {
            // Automatic: transform whenever something is selected, else dictate.
            CommandModeStyle.Automatic =>
                hasSelection ? DictationIntent.CommandAutomatic : DictationIntent.Dictation,

            // Manual: require the extra modifier AND a selection. When the modifier
            // is held but nothing is selected we fall back to plain dictation rather
            // than refusing to record, since selection detection on Windows can be
            // unreliable across apps.
            CommandModeStyle.Manual =>
                manualModifierHeld && hasSelection
                    ? DictationIntent.CommandManual
                    : DictationIntent.Dictation,

            _ => DictationIntent.Dictation,
        };
    }
}
