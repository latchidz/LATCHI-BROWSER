using LatchiBrowser.Core.Services;

namespace LatchiBrowser.Tests;

public class HistoryStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "latchi-tests-" + Guid.NewGuid().ToString("N"));

    private HistoryStore Mk(string profile = "p0123456789") => new(_dir, profile);

    [Fact]
    public void Add_NewestFirst_DedupesConsecutive_CapsSize()
    {
        var s = Mk();
        s.Add("A", "https://a.com/1");
        s.Add("B", "https://b.com/");
        s.Add("B2", "https://b.com/");       // same url right after itself → refresh, no dup
        Assert.Equal(2, s.All.Count);
        Assert.Equal("https://b.com/", s.All[0].Url);
        Assert.Equal("B2", s.All[0].Title);
        Assert.True(s.All[0].VisitedAtUtc >= s.All[1].VisitedAtUtc);
        Assert.All(s.All, h => Assert.Matches("^[0-9a-f]{10}$", h.Id));
    }

    [Fact]
    public void Add_IgnoresNonHttp()
    {
        var s = Mk();
        s.Add("x", "about:blank");
        s.Add("x", "file:///C:/x");
        Assert.Empty(s.All);
    }

    [Fact]
    public void PerProfile_Isolation()
    {
        var a = Mk("pAAAAAAAAA");
        var b = Mk("pBBBBBBBBB");
        a.Add("A", "https://a.com/");
        Assert.Single(a.All);
        Assert.Empty(b.All);
        // files live under Profiles/<id>/ (§80/§81 layout)
        Assert.True(File.Exists(Path.Combine(_dir, "Profiles", "pAAAAAAAAA", "history.json")));
    }

    [Fact]
    public void Search_MatchesTitleAndUrl_CaseInsensitive()
    {
        var s = Mk();
        s.Add("GitHub Projects", "https://github.com/latchidz");
        s.Add("News", "https://news.example.com/");
        Assert.Single(s.Search("github"));
        Assert.Single(s.Search("NEWS"));       // url match, case-insensitive
        Assert.Equal(2, s.Search(null).Count);
        Assert.Equal(2, s.Search("  ").Count);
    }

    [Fact]
    public void RemoveById_And_ClearAll_Persisted()
    {
        var s = Mk();
        s.Add("A", "https://a.com/");
        s.Add("B", "https://b.com/");
        var id = s.All[0].Id;
        Assert.True(s.Remove(id));
        Assert.Single(Mk().All);
        Mk().ClearAll();
        Assert.Empty(Mk().All);
    }

    [Fact]
    public void Cap_LimitsTo2000()
    {
        var s = Mk();
        for (var i = 0; i < 2050; i++)
            s.Add("p" + i, $"https://x.com/{i}");
        Assert.Equal(2000, s.All.Count);
        Assert.Equal("https://x.com/2049", s.All[0].Url);  // newest kept
    }

    [Fact]
    public void CorruptFile_StartsEmpty()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Profiles", "pCCCCCCCCC"));
        File.WriteAllText(Path.Combine(_dir, "Profiles", "pCCCCCCCCC", "history.json"), "nope");
        Assert.Empty(Mk("pCCCCCCCCC").All);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
