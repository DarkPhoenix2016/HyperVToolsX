using System.Windows;
using System.Windows.Threading;
using HyperVToolsX.App.Cli;
using HyperVToolsX.Core.Logging;

namespace HyperVToolsX.App;

public partial class App : Application
{
    /// <summary>Shared rolling file log (per-user %LOCALAPPDATA%\HyperVToolsX\logs).</summary>
    public static FileLogWriter FileLog { get; } = new();

    /// <summary>
    /// With no arguments the main window opens as usual. With any argument the app runs headless
    /// (RVTools-style switches, see <c>/?</c>) and exits with a code instead of showing a window.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RegisterCrashHandlers();

        if (e.Args.Length == 0)
        {
            try
            {
                new MainWindow().Show();
            }
            catch (Exception ex)
            {
                ReportFatal("HyperVToolsX could not start", ex);
                Shutdown(1);
            }

            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        int exitCode;

        try
        {
            exitCode = await CommandLineRunner.RunAsync(e.Args);
        }
        catch (Exception ex)
        {
            LogCrash("Command line run", ex);
            exitCode = 1;
        }

        Shutdown(exitCode);
    }

    private void RegisterCrashHandlers()
    {
        // UI thread: keep the app alive for an error in one handler, but tell the user and log it.
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            ReportFatal("An unexpected error occurred", args.Exception, continueRunning: true);
        };

        // Any other thread: the runtime is going down; make sure the cause reaches the log.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash("Unhandled exception" + (args.IsTerminating ? " (terminating)" : string.Empty), args.ExceptionObject as Exception);

        // Faulted tasks nobody awaited (e.g. a background refresh): log, don't crash.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogCrash("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
    }

    private static void LogCrash(string context, Exception? ex) =>
        FileLog.Write(DateTime.Now, "CRASH", "App", null, $"{context}: {ex}");

    private static void ReportFatal(string title, Exception ex, bool continueRunning = false)
    {
        LogCrash(title, ex);

        try
        {
            MessageBox.Show(
                $"{ex.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"Details were written to:{Environment.NewLine}{FileLog.CurrentFile}" +
                (continueRunning ? string.Empty : $"{Environment.NewLine}{Environment.NewLine}The application will close."),
                title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception)
        {
            // No UI available; the log entry above is what remains.
        }
    }
}
