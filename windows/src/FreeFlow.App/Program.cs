using System;
using System.IO;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace FreeFlow_App;

/// <summary>
/// Custom application entry point. Velopack's <c>VelopackApp.Build().Run()</c> must
/// execute before anything else so install / update / uninstall hooks (shortcut
/// creation, first-run, cleanup) are handled when the packaged Setup invokes the
/// app with its lifecycle arguments. We also enforce a single running instance so
/// the tray-resident app is never launched twice. Replaces the XAML-generated Main
/// (disabled via DISABLE_XAML_GENERATED_MAIN).
/// </summary>
public static class Program
{
    private static Mutex? _singleInstanceMutex;

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            // Handle Velopack lifecycle events (install, update, uninstall, first-run)
            // and exit early when invoked for one of those hooks.
            VelopackApp.Build().Run();

            // Single-instance guard: if another FreeFlow is already running, exit.
            _singleInstanceMutex = new Mutex(initiallyOwned: true, "FreeFlow.SingleInstance", out var isNew);
            if (!isNew)
            {
                return;
            }

            Microsoft.UI.Xaml.Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });

            GC.KeepAlive(_singleInstanceMutex);
        }
        catch (Exception ex)
        {
            // A WinUI bootstrap or DI failure otherwise exits the process with no
            // visible error. Persist the crash so it can be diagnosed post-mortem.
            WriteCrashLog(ex);
            throw;
        }
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FreeFlow");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "crash.log");
            File.AppendAllText(
                path,
                $"[{DateTime.UtcNow:o}] FATAL startup exception{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Never let crash logging mask the original failure.
        }
    }
}
