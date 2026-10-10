using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Models;
using LatchiBrowser.Core.Services;

namespace LatchiBrowser.App.Views;

/// <summary>
/// Per-profile browsing history (§26): search, open, delete one, clear all —
/// all backed by HistoryStore; nothing here is decorative.
/// </summary>
public partial class HistoryWindow : Window
{
    private readonly HistoryStore _store;
    private readonly Func<string, Task> _openUrl; // opens in the browser window (same profile)
    private readonly string _lang;
    private HistoryItem? _selected;

    public HistoryWindow(Window owner, HistoryStore store, Func<string, Task> openUrl, string lang)
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
        Title = Loc.S(_lang, "historyTitle");
        HeaderTitle.Text = Loc.S(_lang, "historyTitle");
        SearchBox.Text = Loc.S(_lang, "historySearch");
        SearchBox.Foreground = (System.Windows.Media.Brush)FindResource("BrushMuted");
        BtnClear.Content = Loc.S(_lang, "historyClear");
        BtnOpen.Content = Loc.S(_lang, "historyOpen");
        BtnDelete.Content = Loc.S(_lang, "historyDelete");
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Refresh();

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchBox.IsKeyboardFocused) Refresh(SearchBox.Text);
    }

    private void Refresh(string? query = null)
    {
        ListHost.Children.Clear();
        _selected = null;

        // the hint text (shown while the box is unfocused) must never filter results
        bool queryIsHint = !SearchBox.IsKeyboardFocused &&
                           SearchBox.Text == Loc.S(_lang, "historySearch");
        var effective = queryIsHint ? null : query;
        var items = _store.Search(effective);

        foreach (var it in items)
        {
            var item = it; // capture per-iteration
            var host = Uri.TryCreate(it.Url, UriKind.Absolute, out var u) ? u.Host : "";
            var row = new Border { Style = (Style)FindResource("ListRow"), Tag = it };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var time = new TextBlock
            {
                Text = it.VisitedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                Foreground = (System.Windows.Media.Brush)FindResource("BrushMuted"),
                FontSize = 11.5,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var title = new TextBlock
            {
                Text = $"{it.Title}  —  {host}",
                Style = (Style)FindResource("BodyText"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(time, 0);
            Grid.SetColumn(title, 1);
            grid.Children.Add(time);
            grid.Children.Add(title);
            row.Child = grid;

            row.MouseLeftButtonUp += (_, _) => { _selected = item; UpdateSelection(row); };
            row.MouseLeftButtonDown += (_, e) => { if (e.ClickCount >= 2) _ = OpenAsync(item); };
            ListHost.Children.Add(row);
        }

        EmptyNote.Text = Loc.S(_lang, "historyEmpty");
        EmptyNote.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BtnOpen.IsEnabled = false;
        BtnDelete.IsEnabled = false;
    }

    private void UpdateSelection(Border selectedRow)
    {
        foreach (var child in ListHost.Children.OfType<Border>())
            child.Background = child == selectedRow
                ? (System.Windows.Media.Brush)FindResource("BrushTabActive")
                : System.Windows.Media.Brushes.Transparent;
        BtnOpen.IsEnabled = _selected is not null;
        BtnDelete.IsEnabled = _selected is not null;
    }

    private async Task OpenAsync(HistoryItem item)
    {
        await _openUrl(item.Url);
        Close();
    }

    private async void OnOpenClick(object sender, RoutedEventArgs e)
    {
        if (_selected is not null) await OpenAsync(_selected);
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _store.Remove(_selected.Id);
        Refresh(SearchBox.IsKeyboardFocused ? SearchBox.Text : null);
    }

    private void OnClearAllClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Loc.S(_lang, "historyConfirmClear"), Loc.S(_lang, "historyTitle"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _store.ClearAll();
        Refresh();
    }
}
