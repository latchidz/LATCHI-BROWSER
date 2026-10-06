using LatchiBrowser.Core.Services;

namespace LatchiBrowser.Tests;

/// <summary>Settings persistence — safe defaults, roundtrip, corrupt-file resilience (§74).</summary>
public class SettingsTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "latchi-browser-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Defaults_AreSafe()
    {
        var dir = TempDir();
        var store = new SettingsStore(dir);
        Assert.Equal("ar", store.Current.Language);
        Assert.Equal(UrlHelper.StartUrl, store.Current.HomePage);   // v1.1: internal start page
        Assert.Equal("google", store.Current.SearchEngineId);
        Assert.False(store.Current.FirstRunCompleted);                  // wizard runs on first launch
    }

    [Fact]
    public void Roundtrip_Persists()
    {
        var dir = TempDir();
        var store = new SettingsStore(dir);
        store.Current.Language = "en";
        store.Current.HomePage = "https://labs.google/fx/tools/flow";
        store.Current.SearchEngineId = "duckduckgo";
        store.Save();

        var reloaded = new SettingsStore(dir);
        Assert.Equal("en", reloaded.Current.Language);
        Assert.Equal("https://labs.google/fx/tools/flow", reloaded.Current.HomePage);
        Assert.Equal("duckduckgo", reloaded.Current.SearchEngineId);
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaults_NeverCrashes()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "settings.json"), "{ this is not json !!!");
        var store = new SettingsStore(dir);
        Assert.Equal("ar", store.Current.Language);          // safe default, browser keeps running
        Assert.Equal(UrlHelper.StartUrl, store.Current.HomePage);
    }

    [Theory]
    [InlineData(null, "ar")]
    [InlineData("ar", "ar")]
    [InlineData("en", "en")]
    [InlineData("fr", "ar")]      // not shipped yet (§77 future) → safe fallback
    [InlineData("garbage", "ar")]
    public void NormalizeLanguage_OnlyKnownValues(string? input, string expected)
        => Assert.Equal(expected, SettingsStore.NormalizeLanguage(input));

    [Theory]
    [InlineData(null, "latchi://start")]
    [InlineData("not a url", "latchi://start")]
    [InlineData("ftp://x.com", "latchi://start")]
    [InlineData("https://x.com", "https://x.com")]
    [InlineData("http://localhost:8080", "http://localhost:8080")]
    [InlineData("latchi://start", "latchi://start")]          // v1.1: internal start page
    [InlineData("LATCHI://START", "latchi://start")]          // case-insensitive
    public void NormalizeHomePage_StartPageOrHttpUrls(string? input, string expected)
        => Assert.Equal(expected, SettingsStore.NormalizeHomePage(input));

    [Theory]
    [InlineData(null, "google")]
    [InlineData("google", "google")]
    [InlineData("bing", "bing")]
    [InlineData("duckduckgo", "duckduckgo")]
    [InlineData("custom", "google")]   // custom engine arrives with the Settings round
    public void NormalizeSearchEngine_OnlyKnownIds(string? input, string expected)
        => Assert.Equal(expected, SettingsStore.NormalizeSearchEngine(input));
}

/// <summary>Search engines (§23) — stable ids, correct query URLs.</summary>
public class SearchEngineTests
{
    [Fact]
    public void Resolve_UnknownFallsBackToGoogle()
    {
        Assert.Equal("google", SearchEngines.Resolve(null).Id);
        Assert.Equal("google", SearchEngines.Resolve("nope").Id);
    }

    [Fact]
    public void All_ContainTheThreeBuiltIns()
    {
        Assert.Equal(new[] { "google", "bing", "duckduckgo" }, SearchEngines.All.Select(e => e.Id).ToArray());
    }

    [Fact]
    public void QueryUrls_AreProperlyEncoded()
    {
        Assert.Contains("q=hello%20world", SearchEngines.Google.BuildSearchUrl("hello world"));
        Assert.Contains("q=hello%20world", SearchEngines.Bing.BuildSearchUrl("hello world"));
        Assert.Contains("q=%D9%85%D8%A7", SearchEngines.DuckDuckGo.BuildSearchUrl("ما"));
    }
}
