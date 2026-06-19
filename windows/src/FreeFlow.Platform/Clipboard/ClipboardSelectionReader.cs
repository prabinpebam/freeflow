using System.Runtime.InteropServices;
using FreeFlow.Core.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Platform.Clipboard;

/// <summary>
/// Baseline selection reader for command/edit mode: synthesizes Ctrl+C to copy
/// the foreground app's current selection, reads the clipboard Unicode text, then
/// restores the prior clipboard contents. This works across the broad set of apps
/// that honour Ctrl+C even when they do not expose UI Automation text patterns
/// (richer UIA-based extraction is a Phase 7 item, see known-limitations.md).
///
/// The clipboard restore runs in a <c>finally</c> so the user's clipboard is
/// returned even if the copy yields nothing. Real OS I/O — exercised at L4; the
/// inner loop uses <c>FakeSelectionReader</c>.
/// </summary>
public sealed class ClipboardSelectionReader : ISelectionReader
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_C = 0x43;

    private readonly ILogger<ClipboardSelectionReader> _logger;
    private readonly TimeSpan _copyDelay;

    public ClipboardSelectionReader(
        ILogger<ClipboardSelectionReader>? logger = null,
        TimeSpan? copyDelay = null)
    {
        _logger = logger ?? NullLogger<ClipboardSelectionReader>.Instance;
        _copyDelay = copyDelay ?? TimeSpan.FromMilliseconds(120);
    }

    public async Task<string?> TryReadSelectionAsync(CancellationToken ct = default)
    {
        var previous = TryGetClipboardText();
        try
        {
            // Clear first so we can tell a genuine copy from "nothing was selected".
            TrySetClipboardText(string.Empty);
            SendCtrlC();
            await Task.Delay(_copyDelay, ct).ConfigureAwait(false);

            var selection = TryGetClipboardText();
            return string.IsNullOrEmpty(selection) ? null : selection;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Selection capture failed.");
            return null;
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

    private static void SendCtrlC()
    {
        var inputs = new INPUT[]
        {
            KeyInput(VK_CONTROL, down: true),
            KeyInput(VK_C, down: true),
            KeyInput(VK_C, down: false),
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
