namespace LatchiBrowser.Core.Models;

/// <summary>
/// LATCHI Browser settings — persisted as JSON (§51). Round 1 carries the fields the
/// engine needs; every later settings round EXTENDS this class (never breaks it).
/// UI language (§77/§78) is the LATCHI UI language only — website language is always
/// decided by the websites themselves, never forced.
/// </summary>
public class AppSettings
{
    /// <summary>LATCHI UI language: ar | en (fr planned later).</summary>
    public string Language { get; set; } = "ar";

    /// <summary>Home page — any absolute http(s) URL (normalized on load).</summary>
    public string HomePage { get; set; } = "https://www.google.com";

    /// <summary>Address-bar search engine id (§23): google | bing | duckduckgo (custom comes with the Settings round).</summary>
    public string SearchEngineId { get; set; } = "google";
}
