using System.Text.RegularExpressions;

namespace LatchiBrowser.Core.Services;

/// <summary>
/// The omnibox brain (§22): turns whatever the user typed into either a real URL or a
/// search-engine query — the exact behavior of a real browser address bar.
/// Pure logic, no UI, fully unit-tested. Never throws.
/// </summary>
public static partial class UrlHelper
{
    [GeneratedRegex(@"^[a-z][a-z0-9+.\-]*://", RegexOptions.IgnoreCase)]
    private static partial Regex SchemeRegex();

    [GeneratedRegex(@"^localhost(:\d+)?(/.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex LocalhostRegex();

    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3}){3}(:\d+)?(/.*)?$")]
    private static partial Regex Ipv4Regex();

    // domain-ish: no spaces/slashes/query chars, at least one dot with a 2+ char tail,
    // optional port/path — "example.com", "sub.example.co.uk", "site.org/x?y".
    // NOTE: dots ARE allowed inside both parts (multi-label hosts) — the \. split is
    // what makes it a domain, not a phrase.
    [GeneratedRegex(@"^[^\s/$?#&=]+\.[^\s/$?#&=]{2,}(:\d+)?(/.*)?$")]
    private static partial Regex DomainRegex();

    // pure numbers ("3.14", "1.2", "42") are searches, not hosts
    [GeneratedRegex(@"^\d{1,3}(\.\d{1,3})?$")]
    private static partial Regex NumberLikeRegex();

    /// <summary>Resolves address-bar input using the default engine (Google).</summary>
    public static string ResolveAddress(string? input, string homePage = "https://www.google.com")
        => ResolveAddress(input, SearchEngines.Google, homePage);

    /// <summary>Resolves address-bar input:
    /// empty → home page; explicit scheme → as-is; localhost/IP → http;
    /// domain-like → https; anything else → search engine query.</summary>
    public static string ResolveAddress(string? input, SearchEngines.Engine engine, string homePage)
    {
        if (string.IsNullOrWhiteSpace(input)) return homePage;
        var s = input.Trim();

        if (string.Equals(s, "about:blank", StringComparison.OrdinalIgnoreCase)) return "about:blank";
        if (SchemeRegex().IsMatch(s)) return s;                    // http://, https://, file://, …
        if (LocalhostRegex().IsMatch(s)) return "http://" + s;     // dev servers
        if (Ipv4Regex().IsMatch(s)) return "http://" + s;          // 192.168.1.1[:port]
        if (!s.Contains(' ')
            && !NumberLikeRegex().IsMatch(s)
            && DomainRegex().IsMatch(s)
            && Uri.TryCreate("https://" + s, UriKind.Absolute, out _))
        {
            return "https://" + s;                                 // example.com → https
        }
        return engine.BuildSearchUrl(s);                           // "best AI video generator" → search
    }
}
