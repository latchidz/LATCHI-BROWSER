using System.Text.Json;

namespace LatchiBrowser.Core.Services;

public class ShortcutItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    /// <summary>Tile color (#AARRGGBB). "google" is reserved for the official G mark.</summary>
    public string Color { get; set; } = "#FF233047";
}

/// <summary>
/// Quick Access shortcuts on the start page (user request): fully editable —
/// add / rename / recolor / remove, persisted in shortcuts.json (corrupt-safe).
/// Ships with a sensible default set; "google" renders the official G mark.
/// </summary>
public class ShortcutStore
{
    private static readonly (string Title, string Url, string Color)[] Defaults =
    {
        ("Google",     "https://www.google.com/",        "google"),
        ("Gmail",      "https://mail.google.com/",       "#FFEA4335"),
        ("YouTube",    "https://www.youtube.com/",       "#FFFF0000"),
        ("Maps",       "https://maps.google.com/",       "#FF34A853"),
        ("Drive",      "https://drive.google.com/",      "#FF1A73E8"),
        ("Gemini",     "https://gemini.google.com/",     "#FF4285F4"),
        ("AI Studio",  "https://aistudio.google.com/",   "#FF7B5EA7"),
        ("Google Flow","https://labs.google/fx/flow",    "#FFE8710A"),
        ("Facebook",   "https://www.facebook.com/",      "#FF1877F2"),
        ("Instagram",  "https://www.instagram.com/",     "#FFE1306C"),
        ("WhatsApp",   "https://web.whatsapp.com/",      "#FF25D366"),
        ("GitHub",     "https://github.com/",            "#FF233047"),
    };

    private static readonly string[] Palette =
    {
        "#FF1A73E8", "#FFE8710A", "#FF34A853", "#FFEA4335",
        "#FF7B5EA7", "#FF00AEC7", "#FFE1306C", "#FF233047",
    };

    private readonly string _path;
    private List<ShortcutItem> _items = new();

    public IReadOnlyList<ShortcutItem> All => _items;

    public ShortcutStore(string dataDir)
    {
        _path = Path.Combine(dataDir, "shortcuts.json");
        // first run (or a corrupt file) → the default tile set.
        // an INTENTIONALLY emptied list (saved as []) is respected, not refilled.
        if (!File.Exists(_path))
        {
            ResetToDefaults();
            return;
        }
        Load();
    }

    private void Load()
    {
        var corrupted = false;
        try
        {
            if (File.Exists(_path))
            {
                var raw = JsonSerializer.Deserialize<List<ShortcutItem>>(File.ReadAllText(_path));
                if (raw is not null) _items = raw;
            }
        }
        catch { corrupted = true; _items = new(); }

        if (corrupted) { ResetToDefaults(); return; }
        // a healthy [] file is a deliberate user choice — keep it empty
        _items = _items
            .Where(s => Uri.TryCreate(s.Url, UriKind.Absolute, out var u)
                        && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
            .Select(s => new ShortcutItem
            {
                Id = string.IsNullOrEmpty(s.Id) ? Guid.NewGuid().ToString("N")[..10] : s.Id,
                Title = string.IsNullOrWhiteSpace(s.Title) ? Uri.TryCreate(s.Url, UriKind.Absolute, out var u2) ? u2.Host : s.Url : s.Title,
                Url = s.Url,
                Color = string.IsNullOrWhiteSpace(s.Color) ? NextColor(0) : s.Color,
            })
            .ToList();
    }

    public ShortcutItem Add(string title, string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u)
            || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("not a valid http(s) url");
        var item = new ShortcutItem
        {
            Id = Guid.NewGuid().ToString("N")[..10],
            Title = string.IsNullOrWhiteSpace(title) ? u.Host : title!.Trim(),
            Url = url!.Trim(),
            Color = NextColor(_items.Count),
        };
        _items.Add(item);
        Save();
        return item;
    }

    public bool Update(string id, string title, string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u)
            || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("not a valid http(s) url");
        var it = _items.FirstOrDefault(s => s.Id == id);
        if (it is null) return false;
        it.Title = string.IsNullOrWhiteSpace(title) ? u.Host : title!.Trim();
        it.Url = url!.Trim();
        Save();
        return true;
    }

    public bool Remove(string id)
    {
        var removed = _items.RemoveAll(s => s.Id == id) > 0;
        if (removed) Save();
        return removed;
    }

    public void ResetToDefaults()
    {
        _items = Defaults.Select((d, i) => new ShortcutItem
        {
            Id = Guid.NewGuid().ToString("N")[..10],
            Title = d.Title,
            Url = d.Url,
            Color = d.Color,
        }).ToList();
        Save();
    }

    private string NextColor(int seed) => Palette[seed % Palette.Length];

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
