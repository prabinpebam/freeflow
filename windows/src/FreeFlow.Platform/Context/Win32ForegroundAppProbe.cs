using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FreeFlow.Core.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Platform.Context;

/// <summary>
/// Identifies the foreground app via <c>GetForegroundWindow</c> → owning process
/// name + window title. Used by <see cref="PolicyAwareSelectionReader"/> to pick a
/// safe selection-extraction strategy (e.g. never synthesize Ctrl+C into a console).
/// Real OS I/O — exercised at L4; the inner loop uses <c>FakeForegroundAppProbe</c>.
/// </summary>
public sealed class Win32ForegroundAppProbe : IForegroundAppProbe
{
    private readonly ILogger<Win32ForegroundAppProbe> _logger;

    public Win32ForegroundAppProbe(ILogger<Win32ForegroundAppProbe>? logger = null)
        => _logger = logger ?? NullLogger<Win32ForegroundAppProbe>.Instance;

    public ForegroundAppInfo? TryGetForegroundApp()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return null;
            }

            _ = GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == 0)
            {
                return null;
            }

            string processName;
            try
            {
                using var process = Process.GetProcessById((int)pid);
                processName = process.ProcessName; // already extension-less
            }
            catch (ArgumentException)
            {
                return null; // process exited between calls
            }

            return new ForegroundAppInfo(processName, GetWindowTitle(hwnd));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Foreground app probe failed.");
            return null;
        }
    }

    private static string? GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return null;
        }

        var buffer = new StringBuilder(length + 1);
        return GetWindowText(hwnd, buffer, buffer.Capacity) > 0 ? buffer.ToString() : null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
}
