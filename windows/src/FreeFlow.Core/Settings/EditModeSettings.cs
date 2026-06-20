using FreeFlow.Core.Input;

namespace FreeFlow.Core.Settings;

/// <summary>
/// How Edit Mode (command mode) is invoked, mirroring the macOS
/// <c>CommandModeStyle</c>: automatically when text is selected, or only when an
/// extra modifier key is held alongside the dictation shortcut.
/// </summary>
public enum CommandModeStyle
{
    /// <summary>Transform the selection whenever one exists at trigger time.</summary>
    Automatic,

    /// <summary>Only transform when the configured manual modifier is held.</summary>
    Manual,
}

/// <summary>
/// The extra modifier that gates manual Edit Mode (parity with the macOS
/// <c>CommandModeManualModifier</c>). Uses Windows-native modifiers.
/// </summary>
public enum CommandModeModifier
{
    Control,
    Alt,
    Shift,
    Windows,
}

/// <summary>
/// User preferences for Edit Mode / command mode (parity with the macOS
/// command-mode settings). When enabled, a dictation trigger on selected text is
/// treated as an instruction to transform that selection rather than to insert
/// new text. Pure data so the intent decision and validation are deterministically
/// testable.
/// </summary>
public sealed record EditModeSettings
{
    /// <summary>Whether Edit Mode is active at all (default off, matching macOS).</summary>
    public bool Enabled { get; init; }

    /// <summary>Automatic (selection present) vs Manual (extra modifier) invocation.</summary>
    public CommandModeStyle Style { get; init; } = CommandModeStyle.Automatic;

    /// <summary>
    /// The extra modifier required in <see cref="CommandModeStyle.Manual"/>.
    /// Defaults to Alt (the Windows analogue of the macOS Option default).
    /// </summary>
    public CommandModeModifier ManualModifier { get; init; } = CommandModeModifier.Alt;
}

/// <summary>Pure helpers mapping <see cref="CommandModeModifier"/> to Win32 primitives.</summary>
public static class CommandModeModifierExtensions
{
    public static string Title(this CommandModeModifier modifier) => modifier switch
    {
        CommandModeModifier.Control => "Ctrl",
        CommandModeModifier.Alt => "Alt",
        CommandModeModifier.Shift => "Shift",
        CommandModeModifier.Windows => "Win",
        _ => modifier.ToString(),
    };

    /// <summary>The shortcut modifier flag this manual modifier corresponds to.</summary>
    public static HotkeyModifiers ToFlag(this CommandModeModifier modifier) => modifier switch
    {
        CommandModeModifier.Control => HotkeyModifiers.Control,
        CommandModeModifier.Alt => HotkeyModifiers.Alt,
        CommandModeModifier.Shift => HotkeyModifiers.Shift,
        CommandModeModifier.Windows => HotkeyModifiers.Windows,
        _ => HotkeyModifiers.None,
    };

    /// <summary>
    /// Virtual-key codes (incl. left/right variants) that represent this modifier,
    /// used to detect modifier-only bindings that would collide with manual Edit Mode.
    /// </summary>
    public static IReadOnlyCollection<int> VirtualKeys(this CommandModeModifier modifier) => modifier switch
    {
        CommandModeModifier.Control => new[] { 0x11, 0xA2, 0xA3 }, // Ctrl, LCtrl, RCtrl
        CommandModeModifier.Alt => new[] { 0x12, 0xA4, 0xA5 },     // Alt, LMenu, RMenu
        CommandModeModifier.Shift => new[] { 0x10, 0xA0, 0xA1 },   // Shift, LShift, RShift
        CommandModeModifier.Windows => new[] { 0x5B, 0x5C },       // LWin, RWin
        _ => Array.Empty<int>(),
    };

    /// <summary>
    /// True when <paramref name="binding"/> already uses this modifier — either as
    /// one of its modifier flags or as a bare modifier-only shortcut. Mirrors the
    /// macOS collision rules that keep the Edit Mode modifier distinct from the
    /// hold/toggle/paste-again shortcuts.
    /// </summary>
    public static bool Collides(this CommandModeModifier modifier, HotkeyCombination binding)
    {
        if (binding.IsUnset)
        {
            return false;
        }

        if (binding.Modifiers.HasFlag(modifier.ToFlag()))
        {
            return true;
        }

        return modifier.VirtualKeys().Contains(binding.VirtualKey);
    }
}
