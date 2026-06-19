using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Context;

namespace FreeFlow.Platform.Context;

/// <summary>
/// Real foreground-window context adapter. Reads the active window's title and
/// owning process via Win32. Selection extraction is deferred to Phase 5, so
/// <see cref="CaptureContext.SelectedText"/> is always null here.
/// </summary>
public sealed class ForegroundWindowContextService : IContextService
{
    public Task<CaptureContext> CaptureAsync(CancellationToken ct = default)
        => Task.FromResult(Capture());

    private static CaptureContext Capture()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return CaptureContext.None;
            }

            var title = GetWindowTitle(hwnd);

            string? processName = null;
            string? appName = null;
            if (GetWindowThreadProcessId(hwnd, out var pid) != 0 && pid != 0)
            {
                try
                {
                    using var process = Process.GetProcessById((int)pid);
                    processName = process.ProcessName;
                    appName = SafeFileDescription(process) ?? process.ProcessName;
                }
                catch (ArgumentException)
                {
                    // Process exited between calls; leave names null.
                }
            }

            return new CaptureContext(appName, processName, title, SelectedText: null);
        }
        catch
        {
            return CaptureContext.None;
        }
    }

    private static string? SafeFileDescription(Process process)
    {
        try
        {
            return process.MainModule?.FileVersionInfo.FileDescription;
        }
        catch
        {
            // Access denied (e.g., elevated target) — fall back to the process name.
            return null;
        }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(length + 1);
        _ = GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
}
