using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using FreeFlow.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Platform.Clipboard;

/// <summary>
/// Real paste adapter implementing the preserve → set → paste → restore flow.
/// Captures the current clipboard Unicode text, places the dictated text,
/// synthesizes Ctrl+V via <c>SendInput</c>, then restores the prior text. The
/// restore runs in a <c>finally</c> so the user's clipboard is returned even if
/// the paste keystroke fails.
///
/// v1 preserves Unicode text only (see known-limitations.md); richer formats are
/// a Phase 7 item. Exercised by the L4 UI tier; the inner loop uses the
/// in-memory paste fake.
/// </summary>
public sealed class Win32ClipboardPasteService : IClipboardPasteService
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    // Modifiers that, if physically held when paste fires (e.g. the Ctrl+Alt of a
    // toggle hotkey), would corrupt the synthesized Ctrl+V chord. We release any
    // that are down before pasting and re-press them afterwards.
    private static readonly ushort[] ModifierKeys =
    {
        0xA0, // VK_LSHIFT
        0xA1, // VK_RSHIFT
        0xA2, // VK_LCONTROL
        0xA3, // VK_RCONTROL
        0xA4, // VK_LMENU (left Alt)
        0xA5, // VK_RMENU (right Alt)
        0x5B, // VK_LWIN
        0x5C, // VK_RWIN
    };

    private readonly ILogger<Win32ClipboardPasteService> _logger;
    private readonly TimeSpan _restoreDelay;

    public Win32ClipboardPasteService(
        ILogger<Win32ClipboardPasteService>? logger = null,
        TimeSpan? restoreDelay = null)
    {
        _logger = logger ?? NullLogger<Win32ClipboardPasteService>.Instance;
        // The restore must not race the asynchronous Ctrl+V: SendInput only queues
        // the keystroke, and the target app reads the clipboard a little later. If
        // we restore the previous clipboard too soon the old text pastes instead.
        _restoreDelay = restoreDelay ?? TimeSpan.FromMilliseconds(400);
    }

    public async Task PasteTextAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var previous = TryGetClipboardText();
        try
        {
            if (!TrySetClipboardText(text))
            {
                _logger.LogWarning("Could not set clipboard text; paste aborted.");
                return;
            }

            SendCtrlV();

            // Give the target app time to consume the paste before we restore.
            await Task.Delay(_restoreDelay, ct).ConfigureAwait(false);
        }
        finally
        {
            if (previous is not null)
            {
                TrySetClipboardText(previous);
            }
        }
    }

    private string? TryGetClipboardText()
    {
        if (!OpenClipboardWithRetry())
        {
            return null;
        }

        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private bool TrySetClipboardText(string text)
    {
        if (!OpenClipboardWithRetry())
        {
            return false;
        }

        try
        {
            EmptyClipboard();

            var bytes = (text.Length + 1) * 2; // UTF-16 + null terminator
            var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hGlobal == IntPtr.Zero)
            {
                return false;
            }

            var target = GlobalLock(hGlobal);
            if (target == IntPtr.Zero)
            {
                GlobalFree(hGlobal);
                return false;
            }

            try
            {
                Marshal.Copy(System.Text.Encoding.Unicode.GetBytes(text + '\0'), 0, target, bytes);
            }
            finally
            {
                GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(CF_UNICODETEXT, hGlobal) == IntPtr.Zero)
            {
                GlobalFree(hGlobal); // ownership not transferred on failure
                return false;
            }

            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool OpenClipboardWithRetry()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
    }

    private void SendCtrlV()
    {
        // Neutralize any modifier keys the user is still physically holding (the
        // Ctrl/Alt of a toggle hotkey, for instance) so the foreground app sees a
        // clean Ctrl+V rather than e.g. Ctrl+Alt+V.
        var held = new List<ushort>();
        foreach (var vk in ModifierKeys)
        {
            if ((GetAsyncKeyState(vk) & 0x8000) != 0)
            {
                held.Add(vk);
            }
        }

        if (held.Count > 0)
        {
            var release = held.Select(vk => KeyInput(vk, down: false)).ToArray();
            SendInput((uint)release.Length, release, Marshal.SizeOf<INPUT>());
        }

        var inputs = new INPUT[]
        {
            KeyInput(VK_CONTROL, down: true),
            KeyInput(VK_V, down: true),
            KeyInput(VK_V, down: false),
            KeyInput(VK_CONTROL, down: false),
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            _logger.LogWarning("SendInput for Ctrl+V reported 0 events (err={Err}).", Marshal.GetLastWin32Error());
        }

        // Restore the modifiers we released so the user's physical hold stays
        // consistent with what the OS believes is down.
        if (held.Count > 0)
        {
            var repress = held.Select(vk => KeyInput(vk, down: true)).ToArray();
            SendInput((uint)repress.Length, repress, Marshal.SizeOf<INPUT>());
        }
    }

    private static INPUT KeyInput(ushort vk, bool down)
    {
        var scan = (ushort)MapVirtualKey(vk, 0);
        var flags = KEYEVENTF_SCANCODE | (down ? 0u : KEYEVENTF_KEYUP);
        if (IsExtendedKey(vk))
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scan,
                    dwFlags = flags,
                },
            },
        };
    }

    private static bool IsExtendedKey(ushort vk) => vk switch
    {
        0xA3 => true, // VK_RCONTROL
        0xA5 => true, // VK_RMENU (right Alt)
        0x5B => true, // VK_LWIN
        0x5C => true, // VK_RWIN
        _ => false,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}

