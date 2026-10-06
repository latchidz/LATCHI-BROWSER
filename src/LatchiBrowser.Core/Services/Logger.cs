namespace LatchiBrowser.Core.Services;

/// <summary>
/// Minimal file logger (§73) — app.log in the data dir.
/// SECURITY (§49): never logs URLs with query strings (hosts only), never passwords,
/// cookies, tokens or API keys. Logging must never crash the browser.
/// </summary>
public static class Logger
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);

    /// <summary>Reduces a URL to its host — the only part we ever log.</summary>
    public static string HostOnly(string? url)
    {
        if (string.IsNullOrEmpty(url)) return "-";
        try { return new Uri(url).Host; }
        catch { return "-"; }
    }

    private static void Write(string level, string message)
    {
        try
        {
            var dir = AppPaths.LogsDir;
            if (!Directory.Exists(dir)) return; // data dir not initialized → stay silent
            lock (Gate)
            {
                File.AppendAllText(Path.Combine(dir, "app.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}\r\n");
            }
        }
        catch
        {
            // logging must never take the browser down
        }
    }
}
