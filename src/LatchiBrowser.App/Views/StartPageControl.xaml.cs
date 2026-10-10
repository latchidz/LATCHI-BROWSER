using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Services;

namespace LatchiBrowser.App.Views;

/// <summary>
/// The LATCHI start page — a real desktop browser home: search, an EDITABLE Quick
/// Access grid (add/edit/remove shortcuts), favorites and recent rows, and a
/// customizable background (dark / solid color / image). No web content here —
/// the webview stays hidden while this page is shown, so it is instant and light.
/// </summary>
public partial class StartPageControl : UserControl
{
    /// <summary>User typed a search / address (raw text — the window resolves it).</summary>
    public event Action<string>? SearchRequested;
    /// <summary>User clicked a shortcut / chip (absolute url).</summary>
    public event Action<string>? NavigateRequested;
    /// <summary>User pressed "+ Add shortcut".</summary>
    public event Action? AddShortcutRequested;
    /// <summary>User asked to edit an existing shortcut (id).</summary>
    public event Action<string>? EditShortcutRequested;
    /// <summary>Background customization (kind: "dark" | "color" | "image" | "remove").</summary>
    public event Action<string>? CustomizeRequested;

    private string _lang = "ar";

    public StartPageControl()
    {
        InitializeComponent();
    }

    public void ApplyLanguage(string lang)
    {
        _lang = lang;
        SearchHint.Text = Loc.S(lang, "startSearchHint");
        QuickAccessTitle.Text = Loc.S(lang, "quickAccess");
        FavoritesTitle.Text = Loc.S(lang, "favoritesRow");
        RecentTitle.Text = Loc.S(lang, "recentRow");
        BtnCustomize.Content = Loc.S(lang, "customizeHome");
    }

    /// <summary>Rebuilds Quick Access from the store (called on every change).</summary>
    public void BindShortcuts(IReadOnlyList<ShortcutItem> shortcuts)
    {
        TilesHost.Children.Clear();

        foreach (var s in shortcuts)
        {
            var url = s.Url;
            var id = s.Id;
            var btn = new Button
            {
                Cursor = Cursors.Hand,
                Focusable = false,
                Margin = new Thickness(9),
                MinWidth = 100,
                MaxWidth = 118,
                Height = 92,
                Background = BrushFrom(s.Color),
                Content = TileContent(s),
                Template = TileTemplate(),
                ToolTip = url,
            };
            btn.Click += (_, _) => NavigateRequested?.Invoke(url);
            btn.MouseRightButtonUp += (_, e) =>
            {
                var menu = new ContextMenu { FontSize = 12.5 };
                var edit = new MenuItem { Header = Loc.S(_lang, "editShortcut") };
                edit.Click += (_, _) => EditShortcutRequested?.Invoke(id);
                var remove = new MenuItem { Header = Loc.S(_lang, "removeShortcut") };
                remove.Click += (_, _) => EditShortcutRequested?.Invoke("remove:" + id);
                menu.Items.Add(edit);
                menu.Items.Add(remove);
                menu.PlacementTarget = btn;
                menu.IsOpen = true;
                e.Handled = true;
            };
            TilesHost.Children.Add(btn);
        }

        // "+ Add shortcut" tile
        var add = new Button
        {
            Cursor = Cursors.Hand,
            Focusable = false,
            Margin = new Thickness(9),
            MinWidth = 100,
            MaxWidth = 118,
            Height = 92,
            Background = new SolidColorBrush(Color.FromArgb(0x55, 0x11, 0x1A, 0x2E)),
            Content = AddTileContent(),
            Template = TileTemplate(),
        };
        add.Click += (_, _) => AddShortcutRequested?.Invoke();
        TilesHost.Children.Add(add);
    }

    /// <summary>Populates the small favorites/recent chip rows (empty rows are hidden).</summary>
    public void BindRows(IReadOnlyList<BookmarkItem> favorites, IReadOnlyList<HistoryItem> recent)
    {
        BindChips(FavoritesHost, FavoritesTitle, favorites.Take(8).Select(b => (b.Title, b.Url)).ToList());
        BindChips(RecentHost, RecentTitle, recent.Take(8).Select(h => (h.Title, h.Url)).ToList());
    }

    private void BindChips(StackPanel host, TextBlock title, List<(string Title, string Url)> items)
    {
        host.Children.Clear();
        title.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        host.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var (titleText, url) in items)
        {
            var chip = new Button
            {
                Style = (Style)FindResource("BookmarkChip"),
                Content = titleText,
                ToolTip = url,
            };
            chip.Click += (_, _) => NavigateRequested?.Invoke(url);
            host.Children.Add(chip);
        }
    }

    private static object TileContent(ShortcutItem s)
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        if (s.Color == "google")
        {
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
            var letter = string.IsNullOrEmpty(s.Title) ? "?" : s.Title.Trim()[0].ToString().ToUpperInvariant();
            panel.Children.Add(new TextBlock
            {
                Text = letter,
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        panel.Children.Add(new TextBlock
        {
            Text = s.Title,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 5, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 100,
        });
        return panel;
    }

    private object AddTileContent()
    {
        var panel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        panel.Children.Add(new TextBlock
        {
            Text = "\uE710", // MDL2 "Add" — vector glyph, no emoji (§84)
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 24,
            Foreground = (Brush)FindResource("BrushMuted"),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = Loc.S(_lang, "addShortcut"),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = (Brush)FindResource("BrushMuted"),
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

    private static Brush BrushFrom(string hex) =>
        hex == "google"
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF))
            : (Brush)new BrushConverter().ConvertFromString(hex);

    private static ControlTemplate TileTemplate()
    {
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

    private void OnCustomizeClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { FontSize = 12.5 };
        void Item(string key, string kind)
        {
            var mi = new MenuItem { Header = Loc.S(_lang, key) };
            mi.Click += (_, _) => CustomizeRequested?.Invoke(kind);
            menu.Items.Add(mi);
        }
        Item("bgDark", "dark");
        Item("bgColor", "color");
        Item("bgImage", "image");
        Item("bgRemove", "remove");
        menu.PlacementTarget = BtnCustomize;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
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
