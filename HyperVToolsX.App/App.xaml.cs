using System.Windows;
using HyperVToolsX.App.Cli;

namespace HyperVToolsX.App;

public partial class App : Application
{
    /// <summary>
    /// With no arguments the main window opens as usual. With any argument the app runs headless
    /// (RVTools-style switches, see <c>/?</c>) and exits with a code instead of showing a window.
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Length == 0)
        {
            new MainWindow().Show();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var exitCode = await CommandLineRunner.RunAsync(e.Args);

        Shutdown(exitCode);
    }
}
