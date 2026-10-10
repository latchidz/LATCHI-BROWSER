using LatchiBrowser.Core.Services;
using Microsoft.Web.WebView2.Core;

namespace LatchiBrowser.App.Browser;

/// <summary>
/// The ONE shared browser engine (§3/§4/§82): a single CoreWebView2Environment for the
/// whole app — every tab, every window and (later) every profile is created from it, so
/// all webviews share the same browser process instead of spawning per-tab runtimes.
/// Created lazily, exactly once. NEVER call WebView2 async APIs with blocking waits —
/// they complete on the UI thread (documented deadlock class).
/// </summary>
public static class BrowserEngine
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static CoreWebView2Environment? _environment;

    /// <summary>Evergreen runtime version, filled by <see cref="IsRuntimeInstalled"/>.</summary>
    public static string? RuntimeVersion { get; private set; }

    /// <summary>Is the Evergreen WebView2 Runtime installed? (§5 — checked before anything else.)</summary>
    public static bool IsRuntimeInstalled()
    {
        try
        {
            RuntimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrEmpty(RuntimeVersion);
        }
        catch
        {
            RuntimeVersion = null;
            return false;
        }
    }

    /// <summary>
    /// The single shared environment. Browser extensions are enabled from day one
    /// (AreBrowserExtensionsEnabled, §28) so the extensions round never needs a data
    /// reset. GPU acceleration stays ON (§67) — we never disable it here.
    /// </summary>
    public static async Task<CoreWebView2Environment> GetEnvironmentAsync(string language)
    {
        if (_environment is not null) return _environment;
        await Gate.WaitAsync();
        try
        {
            if (_environment is not null) return _environment;

            var options = new CoreWebView2EnvironmentOptions
            {
                Language = language == "en" ? "en" : "ar",
                AreBrowserExtensionsEnabled = true,
            };
            _environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: AppPaths.WebViewDataDir,
                options: options);
            Logger.Info("WebView2 environment ready — runtime " + (RuntimeVersion ?? "?"));
            return _environment;
        }
        finally
        {
            Gate.Release();
        }
    }
}
