using System.Runtime.InteropServices;
using FreeFlow.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Platform.Input;

/// <summary>
/// Sends discrete keystrokes to the focused window via <c>SendInput</c> using
/// scan codes. Currently used for the spoken "press enter" command: after the
/// transcript is pasted, a Return key is synthesized so the host app submits.
/// </summary>
public sealed class Win32KeystrokeSender : IKeystrokeSender
{
    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;
    private const ushort VK_RETURN = 0x0D;

    private readonly ILogger<Win32KeystrokeSender> _logger;

    public Win32KeystrokeSender(ILogger<Win32KeystrokeSender>? logger = null)
        => _logger = logger ?? NullLogger<Win32KeystrokeSender>.Instance;

    public Task PressEnterAsync(CancellationToken ct = default)
    {
        var inputs = new[] { KeyInput(VK_RETURN, down: true), KeyInput(VK_RETURN, down: false) };
        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent == 0)
        {
            _logger.LogWarning("SendInput injected 0 events for press-enter (error {Error}).", Marshal.GetLastWin32Error());
        }

        return Task.CompletedTask;
    }

    private static INPUT KeyInput(ushort vk, bool down)
    {
        var scan = (ushort)MapVirtualKey(vk, 0);
        var flags = KEYEVENTF_SCANCODE | (down ? 0u : KEYEVENTF_KEYUP);
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags },
            },
        };
    }

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
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
}
