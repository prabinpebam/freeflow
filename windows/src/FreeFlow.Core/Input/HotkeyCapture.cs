namespace FreeFlow.Core.Input;

/// <summary>
/// Pure helpers for the shortcut-capture UX: turning a captured modifier set and
/// virtual-key code into a <see cref="HotkeyCombination"/>, and detecting clashes
/// between the three configured bindings. The WinUI capture control feeds raw key
/// state in; all decision logic lives here so it can be asserted deterministically.
/// </summary>
public static class HotkeyCapture
{
    /// <summary>
    /// Build a combination from a captured modifier set and primary virtual-key.
    /// A bare modifier press (vk == 0) is promoted to a modifier-only shortcut.
    /// Returns false when nothing usable was captured.
    /// </summary>
    public static bool TryCapture(HotkeyModifiers modifiers, int virtualKey, out HotkeyCombination combination)
    {
        if (virtualKey != 0 && !IsModifierVk(virtualKey))
        {
            combination = new HotkeyCombination(modifiers, virtualKey);
            return true;
        }

        // Only modifiers were held: encode a single modifier as the primary key.
        var promoted = modifiers switch
        {
            HotkeyModifiers.Control => 0xA3, // prefer Right Ctrl for hold-to-talk parity
            HotkeyModifiers.Alt => 0x12,
            HotkeyModifiers.Shift => 0x10,
            HotkeyModifiers.Windows => 0x5B,
            _ => 0,
        };

        if (promoted == 0)
        {
            combination = HotkeyCombination.None;
            return false;
        }

        combination = new HotkeyCombination(modifiers, promoted);
        return true;
    }

    /// <summary>True when two shortcuts would fire on the same key state.</summary>
    public static bool Conflicts(HotkeyCombination a, HotkeyCombination b)
        => !a.IsUnset && !b.IsUnset && a == b;

    /// <summary>
    /// Human-readable descriptions of any pairwise clashes among the three
    /// bindings (empty when all are distinct).
    /// </summary>
    public static IReadOnlyList<string> FindConflicts(HotkeyBindings bindings)
    {
        var named = new (string Name, HotkeyCombination Combo)[]
        {
            ("Hold-to-talk", bindings.HoldToTalk),
            ("Toggle", bindings.Toggle),
            ("Paste-again", bindings.PasteAgain),
        };

        var conflicts = new List<string>();
        for (var i = 0; i < named.Length; i++)
        {
            for (var j = i + 1; j < named.Length; j++)
            {
                if (Conflicts(named[i].Combo, named[j].Combo))
                {
                    conflicts.Add(
                        $"{named[i].Name} and {named[j].Name} both use {named[i].Combo.Format()}.");
                }
            }
        }

        return conflicts;
    }

    private static bool IsModifierVk(int vk)
        => vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
}
