using LatchiBrowser.Core.Services;

namespace LatchiBrowser.Tests;

/// <summary>The omnibox brain (§22) — URL vs search vs home, exactly like a real browser.</summary>
public class UrlHelperTests
{
    private const string Home = "https://www.google.com";

    [Theory]
    [InlineData("example.com", "https://example.com")]
    [InlineData("example.com/path?q=1", "https://example.com/path?q=1")]
    [InlineData("sub.example.co.uk", "https://sub.example.co.uk")]
    [InlineData("example.com:8080/x", "https://example.com:8080/x")]
    [InlineData("https://x.y/z", "https://x.y/z")]                       // explicit scheme → untouched
    [InlineData("http://a.b", "http://a.b")]
    [InlineData("file:///C:/x/y.txt", "file:///C:/x/y.txt")]
    [InlineData("localhost", "http://localhost")]                        // dev servers → http
    [InlineData("localhost:3000", "http://localhost:3000")]
    [InlineData("192.168.1.1", "http://192.168.1.1")]                    // IPs → http
    [InlineData("192.168.1.1:8080/admin", "http://192.168.1.1:8080/admin")]
    [InlineData("about:blank", "about:blank")]
    public void Urls_Navigate(string input, string expected)
        => Assert.Equal(expected, UrlHelper.ResolveAddress(input, Home));

    [Theory]
    [InlineData("hello world")]          // spaces → search
    [InlineData("best AI video generator")]
    [InlineData("gmail")]                // no dot → search
    [InlineData("3.14")]                 // pure number → search, not a host
    [InlineData("1.2")]
    [InlineData("42")]
    [InlineData("ما هذا")]               // Arabic → search, URL-encoded
    public void Queries_Search(string input)
    {
        var result = UrlHelper.ResolveAddress(input, SearchEngines.Google, Home);
        Assert.StartsWith("https://www.google.com/search?q=", result);
        Assert.DoesNotContain(" ", result.Substring("https://www.google.com/search?q=".Length));
    }

    [Theory]
    [InlineData("", Home)]
    [InlineData("   ", Home)]
    [InlineData(null, Home)]
    public void Empty_GoesHome(string? input, string expected)
        => Assert.Equal(expected, UrlHelper.ResolveAddress(input, Home));

    [Fact]
    public void SearchEngine_IsRespected()
    {
        Assert.StartsWith("https://www.bing.com/search?q=",
            UrlHelper.ResolveAddress("hello", SearchEngines.Bing, Home));
        Assert.StartsWith("https://duckduckgo.com/?q=",
            UrlHelper.ResolveAddress("hello", SearchEngines.DuckDuckGo, Home));
    }

    [Fact]
    public void NeverThrows_OnGarbage()
    {
        // a browser omnibox must survive ANY input
        UrlHelper.ResolveAddress("!!@#$%^&*()", SearchEngines.Google, Home);
        UrlHelper.ResolveAddress("javascript:alert(1)", SearchEngines.Google, Home);
        UrlHelper.ResolveAddress("‌‍⁠", SearchEngines.Google, Home); // zero-width garbage
    }
}
