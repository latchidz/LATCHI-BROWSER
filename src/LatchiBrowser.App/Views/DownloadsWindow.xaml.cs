using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LatchiBrowser.App.Services;
using LatchiBrowser.App.Theme;

namespace LatchiBrowser.App.Views;

/// <summary>
/// Live downloads view (§27): every row is a real DownloadItem — Pause/Resume/Cancel
/// call the real CoreWebView2DownloadOperation; Open/Show-in-folder launch the real
/// file/explorer. The list updates live via PropertyChanged (no polling).
/// </summary>
public partial class DownloadsWindow : Window
{
    private readonly string _lang;

    public DownloadsWindow(Window owner, string lang)
    {
        InitializeComponent();
        Owner = owner;
        _lang = lang;
        ApplyLanguage();
        Rebuild();

        // live: new downloads appear / rows update while this window is open
        ((INotifyCollectionChanged)DownloadManager.Items).CollectionChanged += (_, _) => Dispatcher.Invoke(Rebuild);
        Closed += (_, _) => { }; // rows hold no per-window state; GC handles the rest
    }

    private void ApplyLanguage()
    {
        FlowDirection = _lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = Loc.S(_lang, "downloadsTitle");
        HeaderTitle.Text = Loc.S(_lang, "downloadsTitle");
        BtnClear.Content = Loc.S(_lang, "downloadsClear");
    }

    private string StateText(string s) => s switch
    {
        "running" => Loc.S(_lang, "dlRunning"),
        "paused" => Loc.S(_lang, "dlPaused"),
        "done" => Loc.S(_lang, "dlDone"),
        "interrupted" => Loc.S(_lang, "dlInterrupted"),
        _ => s,
    };

    private void Rebuild()
    {
        ListHost.Children.Clear();
        foreach (var item in DownloadManager.Items) AddRow(item);

        if (DownloadManager.Items.Count == 0)
        {
            ListHost.Children.Add(new TextBlock
            {
                Text = Loc.S(_lang, "downloadsEmpty"),
                Style = (Style)FindResource("BodyText"),
                Margin = new Thickness(16, 16, 16, 0),
            });
        }
    }

    private void AddRow(DownloadItem item)
    {
        var row = new Border { Style = (Style)FindResource("ListRow") };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel();

        var title = new TextBlock
        {
            Text = item.FileName,
            Style = (Style)FindResource("BodyText"),
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        left.Children.Add(title);

        var info = new TextBlock
        {
            Foreground = (Brush)FindResource("BrushMuted"),
            FontSize = 11.5,
            FontFamily = new FontFamily("Segoe UI"),
            Margin = new Thickness(0, 2, 0, 0),
        };
        void UpdateInfo()
        {
            var mb = item.Total > 0 ? $" — {item.Received / 1048576.0:F1}/{item.Total / 1048576.0:F1} MB" : "";
            info.Text = $"{StateText(item.State)}{mb} — {item.Url}";
        }
        UpdateInfo();
        left.Children.Add(info);

        var bar = new ProgressBar
        {
            Height = 4,
            Margin = new Thickness(0, 6, 0, 0),
            Maximum = 100,
            Minimum = 0,
            Background = (Brush)FindResource("BrushField"),
            Foreground = (Brush)FindResource("BrushAccent"),
            BorderThickness = new Thickness(0),
            Value = item.ProgressPct,
        };
        left.Children.Add(bar);

        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        // action buttons — visibility bound to what is REALLY possible right now
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        Button Mk(string content, RoutedEventHandler onClick)
        {
            var b = new Button { Content = content, Style = (Style)FindResource("LinkBtn"), Margin = new Thickness(4, 0, 0, 0) };
            b.Click += onClick;
            return b;
        }

        var btnPauseResume = Mk("…", (_, _) =>
        {
            if (item.CanPause) DownloadManager.Pause(item);
            else if (item.CanResume) DownloadManager.Resume(item);
            Rebuild();
        });
        var btnCancel = Mk(Loc.S(_lang, "downloadCancel"), (_, _) => { DownloadManager.Cancel(item); Rebuild(); });
        var btnOpen = Mk(Loc.S(_lang, "downloadOpen"), (_, _) => DownloadManager.OpenFile(item));
        var btnShow = Mk(Loc.S(_lang, "downloadShow"), (_, _) => DownloadManager.ShowInFolder(item));

        void UpdateButtons()
        {
            btnPauseResume.Content = item.CanResume ? Loc.S(_lang, "downloadResume") : Loc.S(_lang, "downloadPause");
            btnPauseResume.IsEnabled = item.CanPause || item.CanResume;
            btnCancel.IsEnabled = item.CanCancel;
            btnOpen.IsEnabled = item.CanOpen;
        }
        UpdateButtons();

        item.PropertyChanged += (_, e) => Dispatcher.Invoke(() =>
        {
            if (e.PropertyName is nameof(DownloadItem.ProgressPct) or nameof(DownloadItem.Received)
                or nameof(DownloadItem.Total) or nameof(DownloadItem.State))
            {
                bar.Value = item.ProgressPct;
                UpdateInfo();
                UpdateButtons();
            }
        });

        actions.Children.Add(btnPauseResume);
        actions.Children.Add(btnCancel);
        actions.Children.Add(btnOpen);
        actions.Children.Add(btnShow);
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        row.Child = grid;
        ListHost.Children.Add(row);
    }

    private void OnClearClick(object sender, RoutedEventArgs e) => DownloadManager.ClearFinished();
}
