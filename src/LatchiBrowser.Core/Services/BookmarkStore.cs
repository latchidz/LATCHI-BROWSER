using System.Text.Json;

namespace LatchiBrowser.Core.Services;

public class BookmarkItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Bookmarks (§24, round-4 scope: add/remove/open + bar). Stored globally in
/// bookmarks.json — corrupt-safe, unit-tested. (Folders/nested come with a later
/// polish round; never faked.)
/// </summary>
public class BookmarkStore
{
    private readonly string _path;
    private List<BookmarkItem> _items = new();

    public BookmarkStore(string dataDir)
    {
        _path = Path.Combine(dataDir, "bookmarks.json");
        Load();
    }

    public IReadOnlyList<BookmarkItem> All => _items;

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _items = JsonSerializer.Deserialize<List<BookmarkItem>>(File.ReadAllText(_path)) ?? new();
        }
        catch { _items = new(); }
        _items = _items
            .Where(b => Uri.TryCreate(b.Url, UriKind.Absolute, out _))
            .Select(b => new BookmarkItem { Id = string.IsNullOrEmpty(b.Id) ? Guid.NewGuid().ToString("N")[..10] : b.Id, Title = string.IsNullOrWhiteSpace(b.Title) ? b.Url : b.Title, Url = b.Url, CreatedAtUtc = b.CreatedAtUtc })
            .ToList();
    }

    public bool Contains(string url) => _items.Any(b => b.Url == url);

    /// <summary>Toggle — returns TRUE if the url was ADDED, FALSE if removed.</summary>
    public bool Toggle(string title, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)
            || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
            return false; // only real pages are bookmarkable

        if (Contains(url))
        {
            _items.RemoveAll(b => b.Url == url);
            Save();
            return false;
        }
        _items.Insert(0, new BookmarkItem
        {
            Id = Guid.NewGuid().ToString("N")[..10],
            Title = string.IsNullOrWhiteSpace(title) ? u.Host : title,
            Url = url,
        });
        Save();
        return true;
    }

    public bool Remove(string url)
    {
        var removed = _items.RemoveAll(b => b.Url == url) > 0;
        if (removed) Save();
        return removed;
    }

    public void Clear()
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
