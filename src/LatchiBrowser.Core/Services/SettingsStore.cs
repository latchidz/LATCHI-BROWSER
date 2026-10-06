using System.Text.Json;
using LatchiBrowser.Core.Models;

namespace LatchiBrowser.Core.Services;

/// <summary>
/// JSON settings store — corrupt-file-safe (safe defaults on any failure, §74-style
/// resilience), atomic enough for a browser, unit-tested. The browser NEVER crashes
/// over settings.
/// </summary>
public class SettingsStore
{
    public AppSettings Current { get; private set; } = new();

    private readonly string _path;

    public SettingsStore(string? dataDir = null)
    {
        _path = dataDir is null
            ? AppPaths.SettingsPath
            : Path.Combine(dataDir, "settings.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new AppSettings();
        }
        catch
        {
            Current = new AppSettings(); // corrupt file → safe defaults, never crash
        }
        // always normalize stored values so the app never sees garbage
        Current.Language = NormalizeLanguage(Current.Language);
        Current.HomePage = NormalizeHomePage(Current.HomePage);
        Current.SearchEngineId = NormalizeSearchEngine(Current.SearchEngineId);
        Current.GeminiModel = string.IsNullOrWhiteSpace(Current.GeminiModel)
            ? Gemini.DefaultModel
            : Current.GeminiModel.Trim();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(_path,
                JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best-effort persistence; the browser keeps running with current values
        }
    }

    public static string NormalizeLanguage(string? lang) => lang is "en" or "ar" ? lang : "ar";

    /// <summary>Accepts any absolute http(s) URL as-is (never rewrites it — the user's
    /// exact home page is respected); anything else falls back to the safe default.</summary>
    public static string NormalizeHomePage(string? url)
    {
        if (Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u)
            && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
            return url!.Trim();
        return "https://www.google.com";
    }

    public static string NormalizeSearchEngine(string? id) =>
        SearchEngines.Resolve(id).Id;
}
