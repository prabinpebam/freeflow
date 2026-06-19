using System.Runtime.InteropServices;
using FreeFlow.Core.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Platform.Input;

/// <summary>
/// Global shortcut adapter built on a low-level keyboard hook
/// (<c>WH_KEYBOARD_LL</c>). Tracks modifier state and the configured primary
/// keys to raise hold-start/hold-stop, toggle, and paste-again triggers.
///
/// The hook must be installed on a thread with a running message loop; in the
/// app that is the WinUI UI thread. This adapter is exercised by the L4 UI tier,
/// not the deterministic inner loop (which uses <c>FakeHotkeyService</c>).
/// </summary>
public sealed class KeyboardHookHotkeyService : IHotkeyService, IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private readonly ILogger<KeyboardHookHotkeyService> _logger;
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hook = IntPtr.Zero;

    private HotkeyBindings _bindings = HotkeyBindings.Defaults;
    private HotkeyModifiers _activeModifiers;
    private bool _holdActive;

    public KeyboardHookHotkeyService(ILogger<KeyboardHookHotkeyService>? logger = null)
    {
        _logger = logger ?? NullLogger<KeyboardHookHotkeyService>.Instance;
        _proc = HookCallback;
    }

    public event Action<HotkeyTrigger>? Triggered;

    public void Configure(HotkeyBindings bindings) => _bindings = bindings;

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        var module = GetModuleHandle(null);
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, module, 0);
        if (_hook == IntPtr.Zero)
        {
            _logger.LogError("Failed to install keyboard hook (error {Error}).", Marshal.GetLastWin32Error());
        }
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }

        _activeModifiers = HotkeyModifiers.None;
        _holdActive = false;
    }

    public void Dispose() => Stop();

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = (int)wParam;
            var vk = Marshal.ReadInt32(lParam); // KBDLLHOOKSTRUCT.vkCode is first field
            var isDown = message is WM_KEYDOWN or WM_SYSKEYDOWN;
            var isUp = message is WM_KEYUP or WM_SYSKEYUP;

            if (isDown || isUp)
            {
                UpdateModifiers(vk, isDown);
                if (isDown)
                {
                    HandleKeyDown(vk);
                }
                else
                {
                    HandleKeyUp(vk);
                }
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void UpdateModifiers(int vk, bool isDown)
    {
        var flag = ModifierFor(vk);
        if (flag == HotkeyModifiers.None)
        {
            return;
        }

        if (isDown)
        {
            _activeModifiers |= flag;
        }
        else
        {
            _activeModifiers &= ~flag;
        }
    }

    private void HandleKeyDown(int vk)
    {
        // Hold-to-talk: begin when the hold combination becomes satisfied.
        if (!_holdActive && Matches(_bindings.HoldToTalk, vk))
        {
            _holdActive = true;
            Raise(HotkeyTrigger.HoldStart);
            return;
        }

        if (Matches(_bindings.Toggle, vk))
        {
            Raise(HotkeyTrigger.Toggle);
            return;
        }

        if (Matches(_bindings.PasteAgain, vk))
        {
            Raise(HotkeyTrigger.PasteAgain);
        }
    }

    private void HandleKeyUp(int vk)
    {
        if (_holdActive && IsHoldKeyRelease(_bindings.HoldToTalk, vk))
        {
            _holdActive = false;
            Raise(HotkeyTrigger.HoldStop);
        }
    }

    private bool Matches(HotkeyCombination combo, int vk)
    {
        if (combo.IsUnset)
        {
            return false;
        }

        // The released/pressed key must be the combo's primary key, and the
        // required modifiers (excluding the primary key itself if it is a
        // modifier) must currently be held.
        if (vk != combo.VirtualKey && !(combo.IsModifierOnly && ModifierFor(vk) != HotkeyModifiers.None && SameModifier(combo, vk)))
        {
            return false;
        }

        return (_activeModifiers & combo.Modifiers) == combo.Modifiers;
    }

    private static bool IsHoldKeyRelease(HotkeyCombination combo, int vk)
        => !combo.IsUnset && (vk == combo.VirtualKey || SameModifier(combo, vk));

    private static bool SameModifier(HotkeyCombination combo, int vk)
        => combo.IsModifierOnly && ModifierFor(vk) != HotkeyModifiers.None && combo.Modifiers.HasFlag(ModifierFor(vk));

    private static HotkeyModifiers ModifierFor(int vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => HotkeyModifiers.Shift,    // VK_SHIFT / L / R
        0x11 or 0xA2 or 0xA3 => HotkeyModifiers.Control,  // VK_CONTROL / L / R
        0x12 or 0xA4 or 0xA5 => HotkeyModifiers.Alt,      // VK_MENU / L / R
        0x5B or 0x5C => HotkeyModifiers.Windows,          // L/R Win
        _ => HotkeyModifiers.None,
    };

    private void Raise(HotkeyTrigger trigger)
    {
        _logger.LogDebug("Hotkey trigger {Trigger}.", trigger);
        Triggered?.Invoke(trigger);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
