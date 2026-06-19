using System;
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
}
