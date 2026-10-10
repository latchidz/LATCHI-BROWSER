using System.Text.Json;

namespace LatchiBrowser.Core.Services;

public class SessionState
{
    /// <summary>The profile this window was last browsing with.</summary>
    public string ActiveProfileId { get; set; } = "";
    /// <summary>Open tabs per profile: profileId → ordered list of urls.</summary>
    public Dictionary<string, List<string>> TabsByProfile { get; set; } = new();
}

/// <summary>
/// Session restore (user request): the open tabs of every profile are saved on exit
/// and restored (LAZILY — webviews are created only when a tab is actually opened)
/// on the next start. session.json, corrupt-safe.
/// </summary>
public class SessionStore
{
    private readonly string _path;

    public SessionStore(string dataDir) =>
        _path = Path.Combine(dataDir, "session.json");

    /// <summary>Loads the saved session. Returns null when there is nothing
    /// meaningful to restore (no file / corrupt / no tabs) — callers then just
    /// open a fresh start tab.</summary>
    public SessionState? Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var s = JsonSerializer.Deserialize<SessionState>(File.ReadAllText(_path));
                if (s is not null)
                {
                    s.TabsByProfile = s.TabsByProfile
                        .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                        .ToDictionary(kv => kv.Key,
                            kv => kv.Value.Where(u => !string.IsNullOrWhiteSpace(u)).ToList());
                    if (s.TabsByProfile.Count == 0) return null;
                    return s;
                }
            }
        }
        catch { /* corrupt → fresh session, never crash */ }
        return null;
    }

    public void Save(SessionState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path,
                JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort persistence */ }
    }

    public void Clear()
    {
        try { if (File.Exists(_path)) File.Delete(_path); }
        catch { /* best effort */ }
    }
}
