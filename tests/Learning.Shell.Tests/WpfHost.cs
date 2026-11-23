using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Learning.Shell.Tests;

/// <summary>
/// One STA thread with a running dispatcher and the program's <see cref="App"/> (its resources), shared
/// by every test that builds a window: WPF allows one Application per process, and xUnit runs test
/// classes in parallel, so they all queue onto this thread.
/// </summary>
internal static class WpfHost
{
    private static readonly object Gate = new();

    private static readonly Lazy<Dispatcher> Host = new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            // App.OnStartup opens no window here (the test host is not the entry assembly).
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WPF test host",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }

    /// <summary>
    /// Runs <paramref name="test"/> on the WPF thread and rethrows whatever it threw — and fails on any
    /// exception the dispatcher saw while it ran (a template that fails during layout, a deferred callback).
    /// </summary>
    public static void Run(Action test)
    {
        Dispatcher dispatcher = Host.Value;
        ExceptionDispatchInfo? failure = null;

        // One test at a time: a test's own pumping would otherwise run another's Invoke in the middle of it.
        lock (Gate)
        {
            dispatcher.Invoke(() =>
            {
                Exception? unhandled = null;
                DispatcherUnhandledExceptionEventHandler handler = (_, e) =>
                {
                    unhandled ??= e.Exception;
                    e.Handled = true;
                };

                dispatcher.UnhandledException += handler;
                try
                {
                    test();
                    Pump();
                }
                catch (Exception ex)
                {
                    failure = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    dispatcher.UnhandledException -= handler;
                }

                if (failure is null && unhandled is not null)
                {
                    failure = ExceptionDispatchInfo.Capture(unhandled);
                }
            });
        }

        failure?.Throw();
    }

    /// <summary>Lets every queued layout, render and deferred callback run (called on the WPF thread).</summary>
    public static void Pump() =>
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
}
