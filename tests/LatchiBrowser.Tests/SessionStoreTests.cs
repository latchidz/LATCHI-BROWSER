using LatchiBrowser.Core.Services;

namespace LatchiBrowser.Tests;

public class SessionStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "latchi-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Save_And_Load_RoundTrips()
    {
        var s = new SessionStore(_dir);
        var state = new SessionState
        {
            ActiveProfileId = "pAAAAAAAAA",
        };
        state.TabsByProfile["pAAAAAAAAA"] =
            new() { "https://github.com/", "https://youtube.com/" };
        state.TabsByProfile["pBBBBBBBBB"] = new() { "https://mail.example/" };
        s.Save(state);

        var loaded = new SessionStore(_dir).Load();
        Assert.NotNull(loaded);
        Assert.Equal("pAAAAAAAAA", loaded!.ActiveProfileId);
        Assert.Equal(2, loaded.TabsByProfile["pAAAAAAAAA"].Count);
        Assert.Equal("https://youtube.com/", loaded.TabsByProfile["pAAAAAAAAA"][1]);
        Assert.Single(loaded.TabsByProfile["pBBBBBBBBB"]);
    }

    [Fact]
    public void Load_MissingOrEmpty_ReturnsNull()
    {
        Assert.Null(new SessionStore(_dir).Load());

        var s = new SessionStore(_dir);
        s.Save(new SessionState()); // no tabs at all
        Assert.Null(new SessionStore(_dir).Load());
    }

    [Fact]
    public void CorruptFile_ReturnsNull_DoesNotThrow()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "session.json"), "[ broken");
        Assert.Null(new SessionStore(_dir).Load());
    }

    [Fact]
    public void Clear_RemovesTheFile()
    {
        var s = new SessionStore(_dir);
        var state = new SessionState { ActiveProfileId = "pX" };
        state.TabsByProfile["pX"] = new() { "https://a.com/" };
        s.Save(state);
        Assert.True(File.Exists(Path.Combine(_dir, "session.json")));

        new SessionStore(_dir).Clear();
        Assert.False(File.Exists(Path.Combine(_dir, "session.json")));
        Assert.Null(new SessionStore(_dir).Load());
    }
}
