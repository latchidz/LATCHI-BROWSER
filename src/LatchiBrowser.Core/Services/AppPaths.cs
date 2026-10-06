namespace LatchiBrowser.Core.Services;

/// <summary>
/// Central data-directory layout (§80/§81). All mutable user data lives under
/// %LOCALAPPDATA%\LATCHI\Browser — NEVER Program Files. The App sets <see cref="DataDir"/>
/// at startup; unit tests never touch it (Logger then silently no-ops).
/// </summary>
public static class AppPaths
{
    private static string? _dataDir;

    /// <summary>App data root. Overridable (tests / portable mode later).</summary>
    public static string DataDir
    {
        get => _dataDir ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LATCHI", "Browser");
        set => _dataDir = value;
    }

    public static string LogsDir => Path.Combine(DataDir, "Logs");
    public static string WebViewDataDir => Path.Combine(DataDir, "WebViewData");
    public static string SettingsPath => Path.Combine(DataDir, "settings.json");

    /// <summary>Creates the directory layout. Returns false (never throws) if inaccessible —
    /// the caller must show a clear error (§5: User Data Folder accessible?).</summary>
    public static bool EnsureDataDir()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(LogsDir);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
