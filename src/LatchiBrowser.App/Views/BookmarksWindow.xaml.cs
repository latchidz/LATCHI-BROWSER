using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Services;

namespace LatchiBrowser.App.Views;

/// <summary>
/// Bookmarks management (§24, current scope): open, remove one, clear all.
/// Adding happens via the star in the toolbar. (Folders arrive later — honestly.)
/// </summary>
public partial class BookmarksWindow : Window
{
    private readonly BookmarkStore _store;
    private readonly Func<string, Task> _openUrl;
    private readonly string _lang;
    private BookmarkItem? _selected;

    public BookmarksWindow(Window owner, BookmarkStore store, Func<string, Task> openUrl, string lang)
    {
        InitializeComponent();
        Owner = owner;
        _store = store;
        _openUrl = openUrl;
        _lang = lang;
        ApplyLanguage();
        Refresh();
    }

    private void ApplyLanguage()
    {
        FlowDirection = _lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = Loc.S(_lang, "bookmarksTitle");
        HeaderTitle.Text = Loc.S(_lang, "bookmarksTitle");
        BtnClear.Content = Loc.S(_lang, "bookmarksClear");
    }

    private void Refresh()
    {
        ListHost.Children.Clear();
        _selected = null;
        var items = _store.All;

        foreach (var it in items)
        {
            var item = it;
            var host = Uri.TryCreate(it.Url, UriKind.Absolute, out var u) ? u.Host : "";
            var row = new Border { Style = (Style)FindResource("ListRow") };
            row.Child = new TextBlock
            {
                Text = $"{it.Title}  —  {host}",
                Style = (Style)FindResource("BodyText"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            row.MouseLeftButtonUp += (_, _) => _selected = item;
            row.MouseLeftButtonDown += (_, e) => { if (e.ClickCount >= 2) { _ = _openUrl(item.Url); Close(); } };
            row.MouseRightButtonUp += (_, e) =>
            {
                var menu = new ContextMenu { FontSize = 12.5 };
                var open = new MenuItem { Header = Loc.S(_lang, "bookmarkOpen") };
                open.Click += async (_, _) => { await _openUrl(item.Url); Close(); };
                var remove = new MenuItem { Header = Loc.S(_lang, "bookmarkRemove") };
                remove.Click += (_, _) => { _store.Remove(item.Url); Refresh(); };
                menu.Items.Add(open);
                menu.Items.Add(remove);
                menu.PlacementTarget = row;
                menu.IsOpen = true;
                e.Handled = true;
            };
            ListHost.Children.Add(row);
        }

        if (items.Count == 0)
            ListHost.Children.Add(new TextBlock
            {
                Text = Loc.S(_lang, "bookmarksBarEmpty"),
                Style = (Style)FindResource("BodyText"),
                Margin = new Thickness(16),
            });
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Loc.S(_lang, "bookmarksConfirm"), Loc.S(_lang, "bookmarksTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _store.Clear();
        Refresh();
    }
}
