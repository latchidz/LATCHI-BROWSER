using System.Windows;
using LatchiBrowser.App.Views;
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

        // a browser must survive UI glitches (§74) — but NEVER silently: log AND tell
        // the user, so "app didn't start" becomes a real, reportable message.
        DispatcherUnhandledException += (_, ex) =>
        {
            var msg = ex.Exception.GetType().Name + ": " + ex.Exception.Message;
            Logger.Error("Unhandled UI exception: " + msg);
            MessageBox.Show(
                Loc_Safe("unexpectedError") + "\n\n" + msg + "\n\nLog: " + AppPaths.LogsDir,
                "LATCHI Browser", MessageBoxButton.OK, MessageBoxImage.Error);
            // only swallow when a window still exists to keep running with
            if (Application.Current.Windows.Count > 0) ex.Handled = true;
            else Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            Logger.Error("Unhandled domain exception: " + ex.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            Logger.Error("Unobserved task exception: " + ex.Exception);
            ex.SetObserved();
        };

        try
        {
            RunStartup();
        }
        catch (Exception ex)
        {
            Logger.Error("startup failed: " + ex);
            MessageBox.Show(
                Loc_Safe("startupFailed") + "\n\n" + ex.GetType().Name + ": " + ex.Message + "\n\nLog: " + AppPaths.LogsDir,
                "LATCHI Browser", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static string Loc_Safe(string key) => key switch
    {
        "unexpectedError" => "An unexpected error occurred",
        "startupFailed" => "LATCHI Browser failed to start",
        _ => key,
    };

    /// <summary>Startup flow: first run → ONE welcome screen (language + Start
    /// Browsing) → browser. Google sign-in is optional and lives INSIDE the browser.
    /// Afterwards → browser directly (persisted sessions, §19).</summary>
    private void RunStartup()
    {
        var settings = new SettingsStore();

        if (!settings.Current.FirstRunCompleted)
        {
            // CRITICAL: while the wizard window closes and before the browser opens
            // there is a moment with zero windows. With OnLastWindowClose the app
            // would start shutting down right there (the v1.1.0 loop). Explicit mode
            // until the browser window is up.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var welcome = LanguageWindow.Ask();
            if (welcome is null) { Shutdown(); return; }
            var (lang, addGoogle) = welcome.Value;
            settings.Current.Language = lang;
            settings.Current.FirstRunCompleted = true;
            settings.Save();
            Logger.Info("first-run welcome completed");

            ShutdownMode = ShutdownMode.OnLastWindowClose;
            var mw = new MainWindow(addGoogleOnStart: addGoogle);
            MainWindow = mw;
            mw.Show();
            mw.Activate();
            return;
        }

        // normal start — the browser window itself
        var mw2 = new MainWindow();
        MainWindow = mw2;
        mw2.Show();
        mw2.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("LATCHI Browser exiting cleanly");
        base.OnExit(e);
    }

    public static string Version =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}
