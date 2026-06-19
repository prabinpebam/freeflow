using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

// Tiny launcher for the FreeFlow portable layout. Lives at the root of the
// distribution as FreeFlow.exe; the real self-contained WinUI app lives in the
// "app" subfolder. A self-contained WinUI app cannot have its DLLs relocated
// (the .NET host resolves framework assemblies by flat paths next to the exe,
// and WinUI's WinRT activation manifest registers its native DLLs next to the
// exe), so this launcher simply starts the app in its own folder. It targets
// .NET Framework 4.x, which is present on all supported Windows versions, so the
// root exe has no extra runtime dependency of its own.
internal static class Launcher
{
    [STAThread]
    private static int Main(string[] args)
    {
        var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
        var appDir = Path.Combine(root, "app");
        var target = Path.Combine(appDir, "FreeFlow.App.exe");

        if (!File.Exists(target))
        {
            // Keep the failure visible without requiring a console window.
            System.Windows.Forms.MessageBox.Show(
                "FreeFlow could not find its application files.\n\nExpected:\n" + target,
                "FreeFlow",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Error);
            return 1;
        }

        var psi = new ProcessStartInfo(target)
        {
            WorkingDirectory = appDir,
            UseShellExecute = false,
        };

        if (args.Length > 0)
        {
            psi.Arguments = string.Join(" ", args);
        }

        Process.Start(psi);
        return 0;
    }
}
