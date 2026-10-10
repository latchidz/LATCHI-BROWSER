using LatchiBrowser.Core.Services;

namespace LatchiBrowser.Tests;

public class BookmarkStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "latchi-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Toggle_AddsThenRemoves_AndPersists()
    {
        var s = new BookmarkStore(_dir);
        Assert.False(s.Contains("https://a.io/"));

        Assert.True(s.Toggle("A page", "https://a.io/"));       // added
        Assert.True(s.Contains("https://a.io/"));
        Assert.False(s.Toggle("A page", "https://a.io/"));      // removed
        Assert.False(s.Contains("https://a.io/"));

        s.Toggle("A page", "https://a.io/");
        var re = new BookmarkStore(_dir);                        // reload — persisted
        Assert.Single(re.All);
        Assert.Equal("A page", re.All[0].Title);
        Assert.Equal("https://a.io/", re.All[0].Url);
    }

    [Fact]
    public void Toggle_IgnoresNonHttpUrls()
    {
        var s = new BookmarkStore(_dir);
        Assert.False(s.Toggle("x", "file:///C:/secret"));
        Assert.False(s.Toggle("x", "javascript:alert(1)"));
        Assert.False(s.Toggle("x", "not a url"));
        Assert.Empty(s.All);
    }

    [Fact]
    public void NewestFirst_AndRemoveByTitleFallback()
    {
        var s = new BookmarkStore(_dir);
        s.Toggle("One", "https://one.com/");
        s.Toggle("Two", "https://two.com/");
        Assert.Equal("Two", s.All[0].Title);   // newest first — bar reads naturally

        Assert.True(s.Remove("https://one.com/"));
        Assert.Single(s.All);
        Assert.False(s.Remove("https://gone.com/"));
    }

    [Fact]
    public void CorruptFile_StartsEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "bookmarks.json"), "]]broken[[");
        var s = new BookmarkStore(_dir);
        Assert.Empty(s.All);
        s.Toggle("x", "https://x.com/");
        Assert.Single(new BookmarkStore(_dir).All);
    }

    [Fact]
    public void Clear_RemovesEverything_Persisted()
    {
        var s = new BookmarkStore(_dir);
        s.Toggle("a", "https://a.com/");
        s.Toggle("b", "https://b.com/");
        s.Clear();
        Assert.Empty(new BookmarkStore(_dir).All);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
