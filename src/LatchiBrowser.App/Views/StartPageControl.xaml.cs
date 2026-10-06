using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LatchiBrowser.App.Theme;

namespace LatchiBrowser.App.Views;

/// <summary>
/// The LATCHI start page (user request 2026-10-06): a real, desktop-native home with a
/// search box and site shortcuts — shown instead of the webview on new tabs / home.
/// No emoji icons (§84): brand-colored letter tiles + the official Google G mark.
/// </summary>
public partial class StartPageControl : UserControl
{
    private sealed class Tile(string label, string url, string bg, string fg, string? letter = null, bool google = false)
    {
        public string Label = label;
        public string Url = url;
        public string Bg = bg;
        public string Fg = fg;
        public string? Letter = letter;
        public bool Google = google;
    }

    private static readonly Tile[] Tiles =
    {
        new("Google",    "https://www.google.com/",        "#FFFFFFFF", "#FF3C4043", google: true),
        new("YouTube",   "https://www.youtube.com/",       "#FFFF0000", "#FFFFFFFF", "Y"),
        new("Gmail",     "https://mail.google.com/",       "#FFEA4335", "#FFFFFFFF", "M"),
        new("Maps",      "https://maps.google.com/",       "#FF34A853", "#FFFFFFFF", "M"),
        new("Drive",     "https://drive.google.com/",      "#FF1A73E8", "#FFFFFFFF", "D"),
        new("Translate", "https://translate.google.com/",  "#FF4285F4", "#FFFFFFFF", "T"),
        new("Facebook",  "https://www.facebook.com/",      "#FF1877F2", "#FFFFFFFF", "f"),
        new("Instagram", "https://www.instagram.com/",     "#FFE1306C", "#FFFFFFFF", "I"),
        new("X",         "https://x.com/",                 "#FF0F1722", "#FFFFFFFF", "X"),
        new("WhatsApp",  "https://web.whatsapp.com/",      "#FF25D366", "#FF10231A", "W"),
        new("GitHub",    "https://github.com/",            "#FF233047", "#FFFFFFFF", "G"),
        new("Wikipedia", "https://www.wikipedia.org/",     "#FFE8EDF6", "#FF101418", "W"),
    };

    /// <summary>The user asked for a search from the start page (raw text — the window resolves it).</summary>
    public event Action<string>? SearchRequested;

    /// <summary>The user clicked a shortcut tile (absolute url).</summary>
    public event Action<string>? NavigateRequested;

    public StartPageControl()
    {
        InitializeComponent();
        BuildTiles();
    }

    public void ApplyLanguage(string lang)
    {
        SearchHint.Text = Loc.S(lang, "startSearchHint");
        SearchBox.ToolTip = Loc.S(lang, "startSearchHint");
    }

    private void BuildTiles()
    {
        foreach (var t in Tiles)
        {
            var url = t.Url;
            var btn = new Button
            {
                Cursor = Cursors.Hand,
                Focusable = false,
                Margin = new Thickness(9),
                Content = TileContent(t),
                Template = TileTemplate(),
                ToolTip = url,
            };
            btn.Click += (_, _) => NavigateRequested?.Invoke(url);
            TilesHost.Children.Add(btn);
        }
    }

    private static object TileContent(Tile t)
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        if (t.Google)
        {
            // the official multicolor Google G (branding guidelines vector)
            panel.Children.Add(new Canvas
            {
                Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center,
                Children =
                {
                    GPath("#FFEA4335", "M16 6.33c2.36 0 4.47.82 6.13 2.4l4.57-4.57C23.94 1.59 20.05 0 16 0 9.75 0 4.34 3.58 1.71 8.8l5.32 4.13C8.28 9.15 11.83 6.33 16 6.33z"),
                    GPath("#FF4285F4", "M31.32 16.36c0-1.06-.1-2.05-.25-3.03H16v6.02h8.63c-.38 1.97-1.5 3.64-3.19 4.77l5.15 4c3.01-2.78 4.73-6.88 4.73-11.76z"),
                    GPath("#FFFBBC05", "M7.02 19.04c-.32-.95-.5-1.96-.5-3.04s.18-2.09.5-3.04l-5.32-4.13C.84 10.97 0 13.4 0 16s.84 5.03 1.71 7.17l5.31-4.13z"),
                    GPath("#FF34A853", "M16 32c4.05 0 7.46-1.34 9.94-3.64l-5.15-4c-1.44.96-3.28 1.55-4.79 1.55-4.17 0-7.72-2.82-8.97-6.6l-5.32 4.13C4.34 28.42 9.75 32 16 32z"),
                },
            });
        }
        else
        {
            panel.Children.Add(new TextBlock
            {
                Text = t.Letter,
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = BrushFrom(t.Fg),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = t.Label,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = BrushFrom(t.Fg),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0),
        });
        return panel;
    }

    private static System.Windows.Shapes.Path GPath(string color, string data) => new()
    {
        Fill = BrushFrom(color),
        Data = Geometry.Parse(data),
    };

    private static Brush BrushFrom(string hex) => (Brush)new BrushConverter().ConvertFromString(hex);

    private static ControlTemplate TileTemplate()
    {
        // plain hover/press template — the per-tile brand color is the Button's Background
        var bd = new FrameworkElementFactory(typeof(Border));
        bd.Name = "bd";
        bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(16));
        bd.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)));
        bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        bd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background")
        {
            RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent),
        });
        bd.SetValue(Border.PaddingProperty, new Thickness(0, 14, 0, 12));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        bd.AppendChild(presenter);

        var gold = new SolidColorBrush(Color.FromRgb(0xE3, 0xB3, 0x41));
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, gold) { TargetName = "bd" });
        var press = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty, Value = true };
        press.Setters.Add(new Setter(UIElement.OpacityProperty, 0.75));

        var template = new ControlTemplate(typeof(Button)) { VisualTree = bd };
        template.Triggers.Add(hover);
        template.Triggers.Add(press);
        return template;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) =>
        SearchHint.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        var text = SearchBox.Text.Trim();
        if (text.Length > 0)
        {
            SearchRequested?.Invoke(text);
            SearchBox.Clear();
        }
        e.Handled = true;
    }
}
