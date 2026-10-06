using LatchiBrowser.Core.Services;

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
    public string HomePage { get; set; } = UrlHelper.StartUrl;   // v1.1: internal start page (search + shortcuts)

    /// <summary>Address-bar search engine id (§23): google | bing | duckduckgo (custom comes with the Settings round).</summary>
    public string SearchEngineId { get; set; } = "google";

    /// <summary>Show the bookmarks bar by default (§25, Ctrl+Shift+B toggles).</summary>
    public bool ShowBookmarksBar { get; set; } = true;

    /// <summary>First-run wizard (language choice + Google sign-in) — false until the
    /// user completes it once; after that the browser opens directly.</summary>
    public bool FirstRunCompleted { get; set; } = false;

    /// <summary>LATCHI AI assistant (§54): disabled until the user provides a key in Settings.</summary>
    public bool GeminiEnabled { get; set; } = false;

    /// <summary>Gemini model id — user-overridable (§55); normalized on load.</summary>
    public string GeminiModel { get; set; } = Gemini.DefaultModel;
}
