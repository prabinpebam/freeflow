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
    private static EventWaitHandle? _showWindowSignal;

    // Process-wide names for the single-instance primitives. The mutex enforces a
    // single running instance; the event lets a second launch ask the first to
    // show its window. Local (per-session) scope is correct for a per-user app.
    private const string SingleInstanceMutexName = "FreeFlow.SingleInstance";
    private const string ShowWindowEventName = "FreeFlow.ShowWindowSignal";

    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            // Handle Velopack lifecycle events (install, update, uninstall, first-run)
            // and exit early when invoked for one of those hooks.
            VelopackApp.Build().Run();

            // Single-instance guard: if another FreeFlow is already running, ask it
            // to surface its window (it may be hidden in the tray) and then exit, so
            // re-launching the app reopens it instead of appearing to do nothing.
            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isNew);
            if (!isNew)
            {
                SignalRunningInstanceToShow();
                return;
            }

            // This is the primary instance: listen for later launches asking us to
            // bring the window forward.
            StartSecondInstanceListener();

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

    private static void SignalRunningInstanceToShow()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(ShowWindowEventName, out var existing))
            {
                existing.Set();
                existing.Dispose();
            }
        }
        catch
        {
            // Best effort: if we cannot signal, simply exiting is still correct.
        }
    }

    private static void StartSecondInstanceListener()
    {
        _showWindowSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        var listener = new Thread(SecondInstanceListenerLoop)
        {
            IsBackground = true,
            Name = "FreeFlow-SingleInstance",
        };
        listener.Start();
    }

    private static void SecondInstanceListenerLoop()
    {
        while (_showWindowSignal is not null)
        {
            try
            {
                _showWindowSignal.WaitOne();
                (Microsoft.UI.Xaml.Application.Current as App)?.ActivateMainWindow();
            }
            catch
            {
                break;
            }
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
