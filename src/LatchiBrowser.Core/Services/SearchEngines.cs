namespace LatchiBrowser.Core.Services;

/// <summary>
/// Address-bar search engines (§23). Round 1 ships the three built-ins; the Settings
/// round adds a Custom engine (user-provided URL template). IDs are stable settings
/// values — never rename them.
/// </summary>
public static class SearchEngines
{
    public sealed record Engine(string Id, string Name, string HomeUrl, Func<string, string> BuildSearchUrl);

    public static readonly Engine Google = new("google", "Google", "https://www.google.com",
        q => "https://www.google.com/search?q=" + Uri.EscapeDataString(q));

    public static readonly Engine Bing = new("bing", "Bing", "https://www.bing.com",
        q => "https://www.bing.com/search?q=" + Uri.EscapeDataString(q));

    public static readonly Engine DuckDuckGo = new("duckduckgo", "DuckDuckGo", "https://duckduckgo.com",
        q => "https://duckduckgo.com/?q=" + Uri.EscapeDataString(q));

    public static IReadOnlyList<Engine> All => new[] { Google, Bing, DuckDuckGo };

    public static Engine Resolve(string? id) => id switch
    {
        "bing" => Bing,
        "duckduckgo" => DuckDuckGo,
        _ => Google,
    };
}
