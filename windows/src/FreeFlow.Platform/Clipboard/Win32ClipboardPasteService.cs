using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using FreeFlow.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Platform.Clipboard;

/// <summary>
/// Real paste adapter implementing the macOS-parity preserve → set → paste →
/// restore flow. Unlike the v1 text-only version, this:
///
/// <list type="bullet">
/// <item>Snapshots <b>every</b> memory-based clipboard format (text, RTF, HTML,
/// DIB images, file lists, …) so restoring never destroys whatever the user had
/// copied — the macOS code preserves the full pasteboard, and a text-only restore
/// would silently wipe an image or rich content.</item>
/// <item>Marks the dictated text as transient so it does <b>not</b> land in
/// Windows clipboard history (Win+V) or roam to the cloud clipboard — the analog
/// of the macOS "transient"/"concealed" pasteboard type marking.</item>
/// <item>Verifies the clipboard sequence number before restoring: if the user (or
/// another app) changed the clipboard during the paste window we skip the restore
/// rather than clobber their new content — the analog of the macOS changeCount
/// check.</item>
/// </list>
///
/// Exercised by the L4 UI tier; the inner loop uses the in-memory paste fake.
/// </summary>
public sealed class Win32ClipboardPasteService : IClipboardPasteService
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    // Handle-based formats that are NOT global-memory blocks; GlobalLock on them is
    // invalid, so they are skipped during snapshot/restore (rare for dictation use).
    private static readonly HashSet<uint> NonMemoryFormats = new()
    {
        2,    // CF_BITMAP
        3,    // CF_METAFILEPICT
        9,    // CF_PALETTE
        14,   // CF_ENHMETAFILE
        0x80, // CF_OWNERDISPLAY
        0x82, // CF_DSPBITMAP
        0x83, // CF_DSPMETAFILEPICT
        0x8E, // CF_DSPENHMETAFILE
    };

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

    // Registered formats that ask the OS to keep this clipboard content out of
    // clipboard history and the cloud clipboard. Resolved lazily once.
    private static readonly uint ExcludeFromHistory =
        RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private static readonly uint ExcludeFromCloud =
        RegisterClipboardFormat("CanUploadToCloudClipboard");
    private static readonly uint ExcludeFromMonitoring =
        RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");

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

        // Full-fidelity snapshot of the prior clipboard so the restore can put back
        // whatever the user had — not just text.
        var snapshot = CaptureClipboardSnapshot();
        var pasteSucceeded = false;
        try
        {
            if (!TrySetClipboardText(text))
            {
                _logger.LogWarning("Could not set clipboard text; paste aborted.");
                return;
            }

            // Remember the sequence number our own write produced so we can detect
            // whether anything changed the clipboard during the paste window.
            var oursSeq = GetClipboardSequenceNumber();

            SendCtrlV();

            // Give the target app time to consume the paste before we restore.
            await Task.Delay(_restoreDelay, ct).ConfigureAwait(false);

            // Only restore if the clipboard still holds *our* content. If the
            // sequence number moved, the user copied something new mid-window and
            // restoring our snapshot would clobber it.
            pasteSucceeded = GetClipboardSequenceNumber() == oursSeq;
        }
        finally
        {
            if (pasteSucceeded)
            {
                RestoreClipboardSnapshot(snapshot);
            }
        }
    }

    private List<ClipboardBlob> CaptureClipboardSnapshot()
    {
        var blobs = new List<ClipboardBlob>();
        if (!OpenClipboardWithRetry())
        {
            return blobs;
        }

        try
        {
            uint format = 0;
            while ((format = EnumClipboardFormats(format)) != 0)
            {
                if (NonMemoryFormats.Contains(format))
                {
                    continue;
                }

                var handle = GetClipboardData(format);
                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                var size = (int)GlobalSize(handle);
                if (size <= 0)
                {
                    continue;
                }

                var ptr = GlobalLock(handle);
                if (ptr == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    var bytes = new byte[size];
                    Marshal.Copy(ptr, bytes, 0, size);
                    blobs.Add(new ClipboardBlob(format, bytes));
                }
                finally
                {
                    GlobalUnlock(handle);
                }
            }
        }
        finally
        {
            CloseClipboard();
        }

        return blobs;
    }

    private void RestoreClipboardSnapshot(List<ClipboardBlob> snapshot)
    {
        if (!OpenClipboardWithRetry())
        {
            return;
        }

        try
        {
            EmptyClipboard();
            foreach (var blob in snapshot)
            {
                PlaceBytes(blob.Format, blob.Data);
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

            var bytes = System.Text.Encoding.Unicode.GetBytes(text + '\0');
            if (!PlaceBytes(CF_UNICODETEXT, bytes))
            {
                return false;
            }

            // Ask Windows to keep the dictated text out of clipboard history and the
            // cloud clipboard. A single zero DWORD is the documented marker payload.
            MarkTransient(ExcludeFromHistory);
            MarkTransient(ExcludeFromCloud);
            MarkTransient(ExcludeFromMonitoring);

            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    private void MarkTransient(uint format)
    {
        if (format == 0)
        {
            return;
        }

        PlaceBytes(format, new byte[] { 0, 0, 0, 0 });
    }

    // Allocates a moveable global block, copies bytes in, and hands ownership to the
    // clipboard. Caller must already hold the clipboard open.
    private bool PlaceBytes(uint format, byte[] bytes)
    {
        var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
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
            Marshal.Copy(bytes, 0, target, bytes.Length);
        }
        finally
        {
            GlobalUnlock(hGlobal);
        }

        if (SetClipboardData(format, hGlobal) == IntPtr.Zero)
        {
            GlobalFree(hGlobal); // ownership not transferred on failure
            return false;
        }

        return true;
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

    private readonly record struct ClipboardBlob(uint Format, byte[] Data);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint EnumClipboardFormats(uint format);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern UIntPtr GlobalSize(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}
