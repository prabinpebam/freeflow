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
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    private readonly ILogger<Win32ClipboardPasteService> _logger;
    private readonly TimeSpan _restoreDelay;

    public Win32ClipboardPasteService(
        ILogger<Win32ClipboardPasteService>? logger = null,
        TimeSpan? restoreDelay = null)
    {
        _logger = logger ?? NullLogger<Win32ClipboardPasteService>.Instance;
        _restoreDelay = restoreDelay ?? TimeSpan.FromMilliseconds(120);
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

    private static void SendCtrlV()
    {
        var inputs = new INPUT[]
        {
            KeyInput(VK_CONTROL, down: true),
            KeyInput(VK_V, down: true),
            KeyInput(VK_V, down: false),
            KeyInput(VK_CONTROL, down: false),
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyInput(ushort vk, bool down) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = vk,
                dwFlags = down ? 0 : KEYEVENTF_KEYUP,
            },
        },
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
