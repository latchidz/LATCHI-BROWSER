using System.Xml.Linq;

namespace LatchiBrowser.Tests;

/// <summary>
/// Regression guard for the classic WPF crash class (learned the hard way on another
/// LATCHI app): a <b>Color</b> resource assigned to a Brush property — XAML compiles
/// fine (StaticResource resolves at RUNTIME) and the app crashes on launch. Also
/// catches references to missing resource keys. Static analysis, runs on any OS.
/// </summary>
public class XamlBrushTests
{
    private static readonly string[] BrushProperties =
        { "BorderBrush", "Background", "Foreground", "Fill", "Stroke", "OpacityMask" };

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0
               && !dir.GetDirectories().Any(d => d.Name == "src"))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string[] AllXaml() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.xaml", SearchOption.AllDirectories);

    private static (HashSet<string> ColorKeys, HashSet<string> AllKeys) CollectKeys()
    {
        var colorKeys = new HashSet<string>();
        var allKeys = new HashSet<string>();
        foreach (var file in AllXaml())
        {
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch { continue; } // malformed XAML fails the build anyway
            foreach (var el in doc.Descendants())
            {
                var key = (string?)el.Attribute(X + "Key");
                if (key is null) continue;
                allKeys.Add(key);
                if (el.Name.LocalName == "Color") colorKeys.Add(key);
            }
        }
        return (colorKeys, allKeys);
    }

    private static string? StaticResourceKey(string? value)
    {
        if (value is null) return null;
        value = value.Trim();
        if (!value.StartsWith("{StaticResource ")) return null;
        return value["{StaticResource ".Length..^1].Trim();
    }

    [Fact]
    public void NoColorResourcesInBrushProperties_AndNoMissingKeys()
    {
        var (colorKeys, allKeys) = CollectKeys();
        Assert.NotEmpty(allKeys);            // the theme actually loaded
        Assert.Empty(colorKeys);             // round 1: theme defines brushes only — keep it that way

        var problems = new List<string>();
        foreach (var file in AllXaml())
        {
            var doc = XDocument.Load(file);
            var shortName = Path.GetFileName(file);
            foreach (var el in doc.Descendants())
            {
                // attribute usage: <Border Background="{StaticResource X}" …>
                foreach (var prop in BrushProperties)
                {
                    var key = StaticResourceKey((string?)el.Attribute(prop));
                    if (key is null) continue;
                    if (colorKeys.Contains(key))
                        problems.Add($"{shortName}: '{prop}' uses Color resource '{key}' (crashes at runtime)");
                    else if (!allKeys.Contains(key))
                        problems.Add($"{shortName}: '{prop}' references missing key '{key}'");
                }

                // style setter usage: <Setter Property="Background" Value="{StaticResource X}"/>
                if (el.Name.LocalName == "Setter")
                {
                    var prop = (string?)el.Attribute("Property");
                    if (prop is null || !BrushProperties.Contains(prop)) continue;
                    var key = StaticResourceKey((string?)el.Attribute("Value"));
                    if (key is null) continue;
                    if (colorKeys.Contains(key))
                        problems.Add($"{shortName}: Setter '{prop}' uses Color resource '{key}' (crashes at runtime)");
                    else if (!allKeys.Contains(key))
                        problems.Add($"{shortName}: Setter '{prop}' references missing key '{key}'");
                }
            }
        }

        Assert.True(problems.Count == 0,
            "XAML resource problems:\n" + string.Join("\n", problems));
    }
}
