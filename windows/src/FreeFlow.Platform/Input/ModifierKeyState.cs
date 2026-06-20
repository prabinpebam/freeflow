using System.Runtime.InteropServices;
using FreeFlow.Core.Settings;

namespace FreeFlow.Platform.Input;

/// <summary>
/// Reads the live pressed state of the Edit Mode manual modifier via
/// <c>GetAsyncKeyState</c>. Used at trigger time (on the keyboard-hook thread) to
/// decide whether a dictation shortcut should be treated as a manual Edit Mode
/// request. Kept tiny and side-effect free so the Core decision stays pure.
/// </summary>
internal static class ModifierKeyState
{
    private const int Pressed = 0x8000;

    public static bool IsHeld(CommandModeModifier modifier)
    {
        foreach (var vk in modifier.VirtualKeys())
        {
            if ((GetAsyncKeyState(vk) & Pressed) != 0)
            {
                return true;
            }
        }

        return false;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
