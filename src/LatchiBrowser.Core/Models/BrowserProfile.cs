namespace LatchiBrowser.Core.Models;

/// <summary>
/// A browser profile (§14/§19): identity + storage mapping. NEVER the email alone —
/// ProfileId is the stable key; Email is just an optional label the user can clear.
/// Each profile maps to a real WebView2 profile (isolated cookies/sessions, §15).
/// </summary>
public class BrowserProfile
{
    public string ProfileId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUsedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>WebView2 ProfileName — a safe, stable folder name derived from the id.</summary>
    public string WebViewProfileName => "p_" + ProfileId;
}
