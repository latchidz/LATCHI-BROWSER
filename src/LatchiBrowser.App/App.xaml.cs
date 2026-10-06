using System.Windows;
using LatchiBrowser.Core.Services;

namespace LatchiBrowser.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // data dir FIRST (§88): settings + logs live under %LOCALAPPDATA%\LATCHI\Browser
        if (!AppPaths.EnsureDataDir())
        {
            MessageBox.Show(
                "Cannot access the application data folder:\n" + AppPaths.DataDir +
                "\n\nCheck disk permissions and try again.",
                "LATCHI Browser", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        Logger.Info($"LATCHI Browser {Version} starting");

        // a browser must survive UI glitches (§74) — log and keep running
        DispatcherUnhandledException += (_, ex) =>
        {
            Logger.Error("Unhandled UI exception: " + ex.Exception.GetType().Name + " — " + ex.Exception.Message);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Logger.Error("Unhandled domain exception: " + ex.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            Logger.Error("Unobserved task exception: " + ex.Exception);
            ex.SetObserved();
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("LATCHI Browser exiting cleanly");
        base.OnExit(e);
    }

    public static string Version =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
}
