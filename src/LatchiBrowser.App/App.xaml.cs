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

    /// <summary>Startup flow: first run → language choice → real Google sign-in →
    /// browser; afterwards → browser directly (persisted sessions, §19).</summary>
    private void RunStartup()
    {
        var settings = new SettingsStore();

        if (!settings.Current.FirstRunCompleted)
        {
            // CRITICAL: during the wizard no window may exist for a moment (between
            // closing one step and opening the next). With OnLastWindowClose the app
            // would START SHUTTING DOWN the instant the language window closes —
            // which made the Google window self-close and the app exit (the exact
            // loop the user hit in v1.1.0). Explicit mode until the browser is up.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 1) professional language gate — the very first thing, ever
            var lang = LanguageWindow.Ask();
            if (lang is null) { Shutdown(); return; }
            settings.Current.Language = lang;
            settings.Save();

            // the default profile must exist BEFORE the sign-in window so both the
            // sign-in webview and the browser share the same isolated session
            var profiles = new ProfileStore(AppPaths.DataDir);
            var profile = profiles.EnsureDefault(
                LatchiBrowser.App.Theme.Loc.S(lang, "profileDefault"));

            // 2) mandatory real Google sign-in, then the browser opens
            var signIn = new GoogleSignInWindow(lang, profile);
            if (signIn.ShowDialog() != true)
            {
                MessageBox.Show(
                    Theme.Loc.S(lang, "mustSignIn"),
                    Theme.Loc.S(lang, "appName"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }
            profiles.Touch(profile.ProfileId);
            settings.Current.FirstRunCompleted = true;
            settings.Save();
            Logger.Info("first-run wizard completed");
        }

        // wizard done (or skipped) — restore normal browser behavior: closing the
        // last browser window closes the app
        ShutdownMode = ShutdownMode.OnLastWindowClose;

        // 3) the browser itself — any construction failure is visible, never silent
        var mw = new MainWindow();
        MainWindow = mw;
        mw.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("LATCHI Browser exiting cleanly");
        base.OnExit(e);
    }

    public static string Version =>
        System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}
