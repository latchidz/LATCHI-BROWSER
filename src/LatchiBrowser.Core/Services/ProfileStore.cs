using System.Text.Json;
using LatchiBrowser.Core.Models;

namespace LatchiBrowser.Core.Services;

/// <summary>
/// Profile persistence (§14-§19): profiles.json in the app data dir. Any number of
/// profiles; each maps to an isolated WebView2 profile. Corrupt file → starts fresh
/// with one default profile (never crashes the browser).
/// </summary>
public class ProfileStore
{
    private readonly string _path;
    private List<BrowserProfile> _profiles = new();

    public IReadOnlyList<BrowserProfile> All => _profiles;

    public ProfileStore(string dataDir)
    {
        _path = Path.Combine(dataDir, "profiles.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _profiles = JsonSerializer.Deserialize<List<BrowserProfile>>(File.ReadAllText(_path)) ?? new();
        }
        catch
        {
            _profiles = new(); // corrupt → fresh start, browser keeps running
        }
        _profiles = _profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.ProfileId) && !string.IsNullOrWhiteSpace(p.DisplayName))
            .ToList();
    }

    /// <summary>Guarantees at least one profile exists (first run) — localized default name.</summary>
    public BrowserProfile EnsureDefault(string displayName)
    {
        if (_profiles.Count > 0) return _profiles[0];
        var p = new BrowserProfile
        {
            ProfileId = NewId(),
            DisplayName = displayName,
        };
        _profiles.Add(p);
        Save();
        return p;
    }

    public BrowserProfile Add(string displayName)
    {
        var p = new BrowserProfile { ProfileId = NewId(), DisplayName = displayName };
        _profiles.Add(p);
        Save();
        return p;
    }

    public bool Remove(string profileId)
    {
        var removed = _profiles.RemoveAll(p => p.ProfileId == profileId) > 0;
        if (removed) Save();
        return removed;
    }

    public BrowserProfile? Find(string profileId) =>
        _profiles.FirstOrDefault(p => p.ProfileId == profileId);

    public void Touch(string profileId)
    {
        if (Find(profileId) is { } p)
        {
            p.LastUsedUtc = DateTime.UtcNow;
            Save();
        }
    }

    public void Rename(string profileId, string displayName, string? email = null)
    {
        if (Find(profileId) is not { } p) return;
        p.DisplayName = displayName.Trim();
        p.Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path,
                JsonSerializer.Serialize(_profiles, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort persistence */ }
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..10];

    public static string NormalizeDisplayName(string? name, int index, string lang)
    {
        var s = name?.Trim();
        if (!string.IsNullOrEmpty(s)) return s!;
        return lang == "en" ? $"Profile {index}" : $"الحساب {index}";
    }
}
