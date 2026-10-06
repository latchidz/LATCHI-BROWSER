using System.Text.Json;

namespace LatchiBrowser.Core.Services;

public class HistoryItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime VisitedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-profile browsing history (§26): DataDir/Profiles/&lt;profileId&gt;/history.json.
/// Capped (2000), consecutive-duplicate collapsing, corrupt-safe. Private windows
/// simply never call Add — that is the whole privacy contract.
/// </summary>
public class HistoryStore
{
    private const int Cap = 2000;
    private readonly string _path;
    private List<HistoryItem> _items = new();

    public HistoryStore(string dataDir, string profileId)
    {
        _path = Path.Combine(dataDir, "Profiles", profileId, "history.json");
        Load();
    }

    public IReadOnlyList<HistoryItem> All => _items;

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _items = JsonSerializer.Deserialize<List<HistoryItem>>(File.ReadAllText(_path)) ?? new();
        }
        catch { _items = new(); }
        _items = _items
            .Where(h => Uri.TryCreate(h.Url, UriKind.Absolute, out _))
            .OrderByDescending(h => h.VisitedAtUtc)
            .ToList();
    }

    /// <summary>Records a visit. Revisiting the same URL right after itself just
    /// refreshes it (no duplicate spam on reloads).</summary>
    public void Add(string title, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)
            || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
            return;
        if (!string.IsNullOrWhiteSpace(title)) title = title.Trim();
        if (string.IsNullOrEmpty(title)) title = u.Host;

        if (_items.Count > 0 && _items[0].Url == url)
        {
            _items[0].Title = title;
            _items[0].VisitedAtUtc = DateTime.UtcNow;
        }
        else
        {
            _items.Insert(0, new HistoryItem
            {
                Id = Guid.NewGuid().ToString("N")[..10],
                Title = title,
                Url = url,
                VisitedAtUtc = DateTime.UtcNow,
            });
        }
        if (_items.Count > Cap) _items.RemoveRange(Cap, _items.Count - Cap);
        Save();
    }

    public IReadOnlyList<HistoryItem> Search(string? query)
    {
        var q = query?.Trim();
        if (string.IsNullOrEmpty(q)) return _items;
        return _items.Where(h =>
            h.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
            || h.Url.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public bool Remove(string id)
    {
        var removed = _items.RemoveAll(h => h.Id == id) > 0;
        if (removed) Save();
        return removed;
    }

    public void ClearAll()
    {
        _items.Clear();
        Save();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path,
                JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* best-effort persistence */ }
    }
}
