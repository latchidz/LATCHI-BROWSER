using LatchiBrowser.Core.Services;

namespace LatchiBrowser.Tests;

public class ShortcutStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "latchi-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Defaults_ContainTwelve_IncludingGoogle()
    {
        var s = new ShortcutStore(_dir);
        Assert.Equal(12, s.All.Count);
        // the Google tile uses the official 4-color G (reserved marker)
        Assert.Contains(s.All, x => x.Color == "google");
        // every default has a real http(s) url — no dead tiles
        Assert.All(s.All, x => Assert.True(
            x.Url.StartsWith("http://") || x.Url.StartsWith("https://"), x.Url));
    }

    [Fact]
    public void Add_Persists_And_Reloads()
    {
        var s = new ShortcutStore(_dir);
        var added = s.Add("MySite", "https://mine.example/");
        Assert.NotEqual("", added.Id);
        Assert.Single(s.All, x => x.Title == "MySite");

        var s2 = new ShortcutStore(_dir); // fresh instance reads the same file
        Assert.Single(s2.All, x => x.Title == "MySite");
        Assert.Equal("https://mine.example/", s2.All.First(x => x.Title == "MySite").Url);
    }

    [Fact]
    public void Update_ChangesTitleAndUrl()
    {
        var s = new ShortcutStore(_dir);
        var a = s.Add("Old", "https://old.example/");
        Assert.True(s.Update(a.Id, "New", "https://new.example/"));
        var reloaded = new ShortcutStore(_dir).All.Single(x => x.Id == a.Id);
        Assert.Equal("New", reloaded.Title);
        Assert.Equal("https://new.example/", reloaded.Url);
    }

    [Fact]
    public void Remove_Works_And_ResetsToDefaults()
    {
        var s = new ShortcutStore(_dir);
        Assert.Equal(12, s.All.Count);
        var victim = s.All[3];
        Assert.True(s.Remove(victim.Id));
        Assert.Equal(11, s.All.Count);
        Assert.DoesNotContain(s.All, x => x.Id == victim.Id);

        s.ResetToDefaults();
        Assert.Equal(12, s.All.Count);
        // reset persisted too
        Assert.Equal(12, new ShortcutStore(_dir).All.Count);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "shortcuts.json"), "{ not json ]");
        var s = new ShortcutStore(_dir);
        Assert.Equal(12, s.All.Count);
    }
}
