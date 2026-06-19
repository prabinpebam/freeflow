using System.Globalization;

namespace FreeFlow.Core.Input;

/// <summary>Modifier keys that can participate in a shortcut.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Control = 1 << 0,
    Alt = 1 << 1,
    Shift = 1 << 2,
    Windows = 1 << 3,
}

/// <summary>
/// Platform-agnostic shortcut definition: a set of modifiers plus a primary
/// key, identified by its Win32 virtual-key code. Parsing/formatting are pure so
/// the shortcut-capture UX and settings round-trip can be asserted deterministically.
/// </summary>
public sealed record HotkeyCombination(HotkeyModifiers Modifiers, int VirtualKey)
{
    /// <summary>A combination that matches nothing (used for "unset").</summary>
    public static readonly HotkeyCombination None = new(HotkeyModifiers.None, 0);

    public bool IsModifierOnly => VirtualKey == 0 || IsModifierKey(VirtualKey);

    public bool IsUnset => Modifiers == HotkeyModifiers.None && VirtualKey == 0;

    /// <summary>Canonical, stable display string (e.g. <c>Ctrl+Alt+Space</c>).</summary>
    public string Format()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");

        var keyName = KeyName(VirtualKey);
        if (!string.IsNullOrEmpty(keyName))
        {
            parts.Add(keyName);
        }

        return parts.Count == 0 ? "(unset)" : string.Join('+', parts);
    }

    public override string ToString() => Format();

    /// <summary>
    /// Parse a canonical shortcut string. Tokens are separated by '+', order
    /// independent, case-insensitive. Throws <see cref="FormatException"/> on an
    /// unrecognized token or a missing primary key.
    /// </summary>
    public static HotkeyCombination Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new FormatException("Shortcut text is empty.");
        }

        var modifiers = HotkeyModifiers.None;
        var key = 0;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    modifiers |= HotkeyModifiers.Control;
                    break;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    break;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    break;
                case "win" or "windows" or "cmd" or "meta":
                    modifiers |= HotkeyModifiers.Windows;
                    break;
                default:
                    if (key != 0)
                    {
                        throw new FormatException($"Shortcut '{text}' has more than one primary key.");
                    }

                    key = ParseKey(raw);
                    break;
            }
        }

        if (key == 0)
        {
            // Modifier-only shortcut (e.g. a bare "Ctrl" hold). Encode the modifier
            // as the primary key so press/release tracking still has a target.
            key = modifiers switch
            {
                HotkeyModifiers.Control => VkControl,
                HotkeyModifiers.Alt => VkMenu,
                HotkeyModifiers.Shift => VkShift,
                HotkeyModifiers.Windows => VkLWin,
                _ => throw new FormatException($"Shortcut '{text}' has no primary key."),
            };
        }

        return new HotkeyCombination(modifiers, key);
    }

    public static bool TryParse(string text, out HotkeyCombination combination)
    {
        try
        {
            combination = Parse(text);
            return true;
        }
        catch (FormatException)
        {
            combination = None;
            return false;
        }
    }

    // A representative subset of Win32 virtual-key codes (winuser.h).
    private const int VkBack = 0x08, VkTab = 0x09, VkReturn = 0x0D, VkShift = 0x10,
        VkControl = 0x11, VkMenu = 0x12, VkEscape = 0x1B, VkSpace = 0x20,
        VkLeft = 0x25, VkUp = 0x26, VkRight = 0x27, VkDown = 0x28,
        VkLWin = 0x5B, VkRControl = 0xA3;

    private static int ParseKey(string token)
    {
        // Single letters A-Z and digits 0-9 map to their ASCII upper value (VK == ASCII).
        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                return c;
            }
        }

        // Function keys F1-F24.
        if ((token.StartsWith('F') || token.StartsWith('f')) && token.Length is 2 or 3
            && int.TryParse(token.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fn)
            && fn is >= 1 and <= 24)
        {
            return 0x70 + (fn - 1); // VK_F1 == 0x70
        }

        return token.ToLowerInvariant() switch
        {
            "space" => VkSpace,
            "enter" or "return" => VkReturn,
            "tab" => VkTab,
            "esc" or "escape" => VkEscape,
            "backspace" or "back" => VkBack,
            "left" => VkLeft,
            "right" => VkRight,
            "up" => VkUp,
            "down" => VkDown,
            "rightctrl" or "rctrl" => VkRControl,
            _ => throw new FormatException($"Unrecognized key token '{token}'."),
        };
    }

    private static string KeyName(int vk) => vk switch
    {
        0 => string.Empty,
        VkSpace => "Space",
        VkReturn => "Enter",
        VkTab => "Tab",
        VkEscape => "Esc",
        VkBack => "Backspace",
        VkLeft => "Left",
        VkRight => "Right",
        VkUp => "Up",
        VkDown => "Down",
        VkRControl => "RightCtrl",
        VkControl => "Ctrl",
        VkMenu => "Alt",
        VkShift => "Shift",
        VkLWin => "Win",
        >= 'A' and <= 'Z' => ((char)vk).ToString(),
        >= '0' and <= '9' => ((char)vk).ToString(),
        >= 0x70 and <= 0x87 => "F" + (vk - 0x70 + 1).ToString(CultureInfo.InvariantCulture),
        _ => "0x" + vk.ToString("X2", CultureInfo.InvariantCulture),
    };

    private static bool IsModifierKey(int vk)
        => vk is VkControl or VkMenu or VkShift or VkLWin or VkRControl or 0xA0 or 0xA1 or 0xA2 or 0xA4 or 0xA5 or 0x5C;
}
