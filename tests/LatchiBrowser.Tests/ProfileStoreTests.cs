using LatchiBrowser.Core.Services;

namespace LatchiBrowser.Tests;

public class ProfileStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "latchi-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureDefault_CreatesFirstProfile_AndPersists()
    {
        var s = new ProfileStore(_dir);
        Assert.Empty(s.All);                       // fresh dir → nothing yet
        var p = s.EnsureDefault("Personal");
        Assert.Single(s.All);
        Assert.Equal("Personal", p.DisplayName);
        Assert.Matches("^[0-9a-f]{10}$", p.ProfileId);
        Assert.StartsWith("p_", p.WebViewProfileName); // safe WebView2 profile name

        // reload → still exactly one, same id (sessions survive restarts, §19)
        var s2 = new ProfileStore(_dir);
        Assert.Single(s2.All);
        Assert.Equal(p.ProfileId, s2.All[0].ProfileId);
        // second EnsureDefault is a no-op
        Assert.Same(s2.All[0], s2.EnsureDefault("x"));
        Assert.Single(s2.All);
    }

    [Fact]
    public void Add_Remove_Touch_Work()
    {
        var s = new ProfileStore(_dir);
        var first = s.EnsureDefault("A");
        var b = s.Add("B");
        Assert.Equal(2, s.All.Count);
        Assert.True(Uri.IsWellFormedUriString("file:///" + b.WebViewProfileName, UriKind.RelativeOrAbsolute)
                    || !b.WebViewProfileName.Contains(Path.DirectorySeparatorChar));

        var before = b.LastUsedUtc;
        Thread.Sleep(20);
        s.Touch(b.ProfileId);
        Assert.True(s.Find(b.ProfileId)!.LastUsedUtc > before);

        Assert.True(s.Remove(b.ProfileId));
        Assert.Single(new ProfileStore(_dir).All);          // persisted removal
        Assert.False(s.Remove("nope"));
        Assert.Null(s.Find("nope"));
        Assert.Same(first, s.Find(first.ProfileId));
    }

    [Fact]
    public void Rename_PersistsNameAndClearsEmail()
    {
        var s = new ProfileStore(_dir);
        var p = s.EnsureDefault("A");
        s.Rename(p.ProfileId, "  Work  ", " me@x.com ");
        var re = new ProfileStore(_dir).Find(p.ProfileId)!;
        Assert.Equal("Work", re.DisplayName);
        Assert.Equal("me@x.com", re.Email);
        s.Rename(p.ProfileId, "Work", "   ");
        Assert.Null(new ProfileStore(_dir).Find(p.ProfileId)!.Email);
    }

    [Fact]
    public void CorruptFile_StillStarts_WithEmpty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "profiles.json"), "{ not json !!");
        var s = new ProfileStore(_dir);
        Assert.Empty(s.All);                            // browser never dies on bad data
        var p = s.EnsureDefault("Personal");
        Assert.Single(new ProfileStore(_dir).All);      // and can start over
    }

    [Fact]
    public void NormalizeDisplayName_FallsBackPerLanguage()
    {
        Assert.Equal("Work", ProfileStore.NormalizeDisplayName(" Work ", 2, "ar"));
        Assert.Equal("الحساب 3", ProfileStore.NormalizeDisplayName("  ", 3, "ar"));
        Assert.Equal("Profile 4", ProfileStore.NormalizeDisplayName(null, 4, "en"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
