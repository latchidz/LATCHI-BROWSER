using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shell;
using LatchiBrowser.App.Browser;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LatchiBrowser.App.Views;

/// <summary>
/// LATCHI Browser main window (§6/§7): custom chrome where the tab strip IS the caption,
/// real window controls, real tabs, real navigation. Every button that exists here is
/// wired to a real function (§107) — features that are not built yet simply have no
/// button yet.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private readonly List<string> _recentlyClosed = new();   // Ctrl+Shift+T (§11)
    private const int RecentlyClosedCap = 25;

    private SettingsStore _settings = new();
    private string _lang = "ar";
    private CoreWebView2Environment? _env;
    private BrowserTab? _active;
    private bool _appFullscreen;        // F11 (browser fullscreen)
    private bool _contentFullscreen;    // video/website fullscreen requested by a page (§8)
    private WindowChrome? _normalChrome;
    private WindowState _stateBeforeFullscreen = WindowState.Normal;
    private bool _addressHintOn;

    public MainWindow()
    {
        InitializeComponent();
        TabsControl.ItemsSource = _tabs;
        // tunneling, handled-too: keys reach us even when the WebView has focus
        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKey), true);
        Closing += (_, _) => CleanupTabs();
    }

    private string Home => SettingsStore.NormalizeHomePage(_settings.Current.HomePage);
    private SearchEngines.Engine SearchEngine => SearchEngines.Resolve(_settings.Current.SearchEngineId);

    /* ═════════════════ startup (§87/§88: window first, engine after) ═════════════════ */

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings = new SettingsStore();
        _lang = SettingsStore.NormalizeLanguage(_settings.Current.Language);
        ApplyLanguage();

        // §5: runtime check BEFORE anything — with a clear message + official installer
        if (!BrowserEngine.IsRuntimeInstalled())
        {
            Logger.Error("WebView2 runtime missing — install panel shown");
            ShowFatal(Loc.S(_lang, "errRuntimeTitle"), Loc.S(_lang, "errRuntimeDetail"),
                Loc.S(_lang, "openDownload"), openRuntimeDownload: true);
            return;
        }
        Logger.Info("WebView2 runtime " + BrowserEngine.RuntimeVersion);

        // shared engine — the window is already on screen while this warms up
        try
        {
            _env = await BrowserEngine.GetEnvironmentAsync(_lang);
        }
        catch (Exception ex)
        {
            Logger.Error("engine init failed: " + ex.Message);
            ShowFatal(Loc.S(_lang, "errRuntimeTitle"),
                Loc.S(_lang, "errRuntimeDetail") + "\n\n(" + ex.Message + ")",
                Loc.S(_lang, "openDownload"), openRuntimeDownload: true);
            return;
        }

        await NewTabAsync();
        AddressBox.Focus();
    }

    /* ═════════════════ tabs (§11/§12) ═════════════════ */

    private async Task NewTabAsync(string? url = null)
    {
        if (_env is null) return; // engine not ready yet — button is honest but inert

        var tab = new BrowserTab(_lang);
        // popup → new tab (§47). NOTE: parameter is 't' — a '_' here would capture as
        // BrowserTab inside the inner lambda and break the discard (real compiler trap).
        tab.PopupRequested += (t, uri) => Dispatcher.Invoke(() => _ = NewTabAsync(uri));
        tab.ContentFullscreenChanged += (_, on) => Dispatcher.Invoke(() =>
        {
            _contentFullscreen = on;
            if (on) EnterFullscreen(hideChrome: false);
            else ExitFullscreenIfIdle();
        });
        tab.PropertyChanged += (s, e) => Dispatcher.Invoke(() =>
            OnTabPropertyChanged((BrowserTab)s!, e.PropertyName!));

        _tabs.Add(tab);
        var start = url ?? Home;
        await tab.InitializeAsync(_env, start);
        if (tab.WebView is not null) ContentHost.Children.Add(tab.WebView);
        ActivateTab(tab);
    }

    private void ActivateTab(BrowserTab tab)
    {
        _active = tab;
        tab.Touch();
        foreach (var t in _tabs) t.IsActive = t == tab;
        foreach (var child in ContentHost.Children.OfType<WebView2>())
            child.Visibility = child == tab.WebView ? Visibility.Visible : Visibility.Collapsed;
        SyncToolbar();
    }

    private void CloseTab(BrowserTab tab)
    {
        var idx = _tabs.IndexOf(tab);
        if (idx < 0) return;

        // remember the address for Ctrl+Shift+T (only real pages)
        if (Uri.TryCreate(tab.Address, UriKind.Absolute, out var u)
            && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
        {
            _recentlyClosed.Add(u.ToString());
            while (_recentlyClosed.Count > RecentlyClosedCap) _recentlyClosed.RemoveAt(0);
        }

        _tabs.RemoveAt(idx);
        if (tab.WebView is not null) ContentHost.Children.Remove(tab.WebView);
        tab.Dispose();

        if (_tabs.Count == 0)
        {
            Close(); // real browser: closing the last tab closes the window
            return;
        }
        if (_active == tab) ActivateTab(_tabs[Math.Min(idx, _tabs.Count - 1)]);
    }

    private void ReopenClosedTab()
    {
        if (_recentlyClosed.Count == 0) return;
        var url = _recentlyClosed[^1];
        _recentlyClosed.RemoveAt(_recentlyClosed.Count - 1);
        _ = NewTabAsync(url);
    }

    private void CycleTab(int delta)
    {
        if (_tabs.Count < 2 || _active is null) return;
        var i = _tabs.IndexOf(_active);
        ActivateTab(_tabs[(i + delta + _tabs.Count) % _tabs.Count]);
    }

    private void CleanupTabs()
    {
        foreach (var t in _tabs) t.Dispose();
        _tabs.Clear();
    }

    /* ═════════════════ toolbar ↔ active tab ═════════════════ */

    private void OnTabPropertyChanged(BrowserTab tab, string prop)
    {
        if (tab != _active) return;
        switch (prop)
        {
            case nameof(BrowserTab.Address): UpdateAddressBox(); break;
            case nameof(BrowserTab.CanGoBack): BtnBack.IsEnabled = tab.CanGoBack; break;
            case nameof(BrowserTab.CanGoForward): BtnForward.IsEnabled = tab.CanGoForward; break;
            case nameof(BrowserTab.IsLoading): UpdateReloadStop(); break;
        }
    }

    private void SyncToolbar()
    {
        BtnBack.IsEnabled = _active?.CanGoBack == true;
        BtnForward.IsEnabled = _active?.CanGoForward == true;
        UpdateReloadStop();
        UpdateAddressBox();
    }

    private void UpdateReloadStop()
    {
        var loading = _active?.IsLoading == true;
        BtnReloadStop.Content = loading ? "\uE71A" : "\uE72C"; // Cancel : Refresh
        BtnReloadStop.ToolTip = Loc.S(_lang, loading ? "navStop" : "navReload");
    }

    private void UpdateAddressBox()
    {
        if (_active is null) return;
        if (!AddressBox.IsKeyboardFocused && !_addressHintOn)
            AddressBox.Text = _active.Address;
        LockGlyph.Text = _active.Address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? "\uE72E"     // lock — secure (§50)
            : "\uE7BA";    // warning glyph — not https
        LockGlyph.Foreground = _active.Address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? (System.Windows.Media.Brush)FindResource("BrushMuted")
            : (System.Windows.Media.Brush)FindResource("BrushDanger");
    }

    /* ═════════════════ navigation (§22) ═════════════════ */

    private void OnAddressKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _addressHintOn = false;
        AddressBox.Foreground = (System.Windows.Media.Brush)FindResource("BrushText");
        var resolved = UrlHelper.ResolveAddress(AddressBox.Text, SearchEngine, Home);
        if (_active is not null)
        {
            _active.Navigate(resolved);
            _active.WebView?.Focus(); // keys belong to the page again
        }
        e.Handled = true;
    }

    private void OnAddressFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        ClearAddressHint();
        AddressBox.SelectAll();
    }

    private void ShowAddressHint(string text)
    {
        if (AddressBox.IsKeyboardFocused) return;
        _addressHintOn = true;
        AddressBox.Text = text;
        AddressBox.Foreground = (System.Windows.Media.Brush)FindResource("BrushMuted");
    }

    private void ClearAddressHint()
    {
        if (!_addressHintOn) return;
        _addressHintOn = false;
        AddressBox.Text = "";
        AddressBox.Foreground = (System.Windows.Media.Brush)FindResource("BrushText");
    }

    /* ═════════════════ click handlers — every one is a real function (§107) ═════════════════ */

    private void OnBackClick(object sender, RoutedEventArgs e) => _active?.GoBack();
    private void OnForwardClick(object sender, RoutedEventArgs e) => _active?.GoForward();

    private void OnReloadStopClick(object sender, RoutedEventArgs e)
    {
        if (_active is null) return;
        if (_active.IsLoading) _active.Stop();
        else _active.Reload();
    }

    private void OnHomeClick(object sender, RoutedEventArgs e) => _active?.Navigate(Home);
    private async void OnNewTabClick(object sender, RoutedEventArgs e) => await NewTabAsync();

    private void OnTabClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BrowserTab t }) ActivateTab(t);
    }

    private void OnTabMouseDown(object sender, MouseButtonEventArgs e)
    {
        // middle-click closes the tab — the way every real browser works
        if (e.ChangedButton == MouseButton.Middle && sender is FrameworkElement { DataContext: BrowserTab t })
        {
            CloseTab(t);
            e.Handled = true;
        }
    }

    private void OnTabClose(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BrowserTab t }) CloseTab(t);
    }

    private void OnMenuClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { FontSize = 12.5 };
        var miNew = new MenuItem { Header = Loc.S(_lang, "newTabBtn") };
        miNew.Click += async (_, _) => await NewTabAsync();
        var miReopen = new MenuItem { Header = Loc.S(_lang, "reopenTab"), IsEnabled = _recentlyClosed.Count > 0 };
        miReopen.Click += (_, _) => ReopenClosedTab();
        var miFs = new MenuItem { Header = Loc.S(_lang, "fullscreen") };
        miFs.Click += (_, _) => ToggleAppFullscreen();
        var miLang = new MenuItem { Header = Loc.S(_lang, "langSwitch") };
        miLang.Click += (_, _) => ToggleLanguage();
        var miAbout = new MenuItem { Header = Loc.S(_lang, "menuAbout") };
        miAbout.Click += (_, _) => ShowAbout();
        var miExit = new MenuItem { Header = Loc.S(_lang, "menuExit") };
        miExit.Click += (_, _) => Close();
        foreach (var mi in new[] { miNew, miReopen, miFs, miLang, miAbout, miExit }) menu.Items.Add(mi);

        menu.PlacementTarget = BtnMenu;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void ShowAbout()
    {
        MessageBox.Show(this,
            string.Format(Loc.S(_lang, "menuAbout") + "\n\nv{0}\nWebView2 Runtime: {1}\n© 2026 LATCHI",
                App.Version, BrowserEngine.RuntimeVersion ?? "-"),
            "LATCHI Browser", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /* ═════════════════ window controls (§7) + chrome dragging ═════════════════ */

    private void OnWinMinClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnWinMaxClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnWinCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnWindowStateChanged(object sender, EventArgs e) =>
        BtnWinMax.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922"; // Restore : Maximize

    private void OnToolbarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* drag races are harmless */ }
        }
    }

    /* ═════════════════ fullscreen — three distinct cases (§8) ═════════════════
       browser fullscreen (F11): chrome hidden, borderless, maximized
       content fullscreen (video/website): borderless, chrome hidden — driven by the page
       window maximize: normal OS maximize, chrome stays                                   */

    private void OnToggleFullscreenClick(object sender, RoutedEventArgs e) => ToggleAppFullscreen();

    private void ToggleAppFullscreen()
    {
        if (_appFullscreen)
        {
            _appFullscreen = false;
            ExitFullscreenIfIdle();
        }
        else
        {
            _appFullscreen = true;
            EnterFullscreen(hideChrome: true);
        }
    }

    private void EnterFullscreen(bool hideChrome)
    {
        if (hideChrome) ChromeArea.Visibility = Visibility.Collapsed;
        if (_normalChrome is null) _normalChrome = WindowChrome.GetWindowChrome(this);
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });
        if (WindowState != WindowState.Maximized)
        {
            _stateBeforeFullscreen = WindowState;
            WindowState = WindowState.Maximized;
        }
    }

    private void ExitFullscreenIfIdle()
    {
        // restore only when BOTH browser-F11 and page-driven fullscreen are off
        if (_appFullscreen || _contentFullscreen) return;
        ChromeArea.Visibility = Visibility.Visible;
        if (_normalChrome is not null) WindowChrome.SetWindowChrome(this, _normalChrome);
        if (_stateBeforeFullscreen == WindowState.Normal) WindowState = WindowState.Normal;
    }

    /* ═════════════════ keyboard (§11) ═════════════════ */

    private void OnPreviewKey(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

        if (e.Key == Key.F11) { ToggleAppFullscreen(); e.Handled = true; return; }
        if (e.Key == Key.Escape && _appFullscreen && !_contentFullscreen) { ToggleAppFullscreen(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.T && !shift) { _ = NewTabAsync(); e.Handled = true; return; }
        if (ctrl && shift && e.Key == Key.T) { ReopenClosedTab(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.W) { if (_active is not null) CloseTab(_active); e.Handled = true; return; }
        if (ctrl && e.Key == Key.Tab) { CycleTab(shift ? -1 : +1); e.Handled = true; return; }
        if (ctrl && e.Key == Key.L) { AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.R) { _active?.Reload(); e.Handled = true; return; }
        if (e.Key == Key.F5) { _active?.Reload(); e.Handled = true; return; }
        if (alt && e.Key == Key.Left) { _active?.GoBack(); e.Handled = true; return; }
        if (alt && e.Key == Key.Right) { _active?.GoForward(); e.Handled = true; return; }
    }

    /* ═════════════════ language (§77/§78: UI language only) ═════════════════ */

    private void OnToggleLanguage(object sender, RoutedEventArgs e) => ToggleLanguage();

    private void ToggleLanguage()
    {
        _lang = _lang == "ar" ? "en" : "ar";
        _settings.Current.Language = _lang;
        _settings.Save();
        ApplyLanguage();
        Logger.Info("UI language → " + _lang);
    }

    private void ApplyLanguage()
    {
        FlowDirection = _lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = Loc.S(_lang, "appName");
        BtnNewTab.ToolTip = Loc.S(_lang, "newTabBtn");
        BtnLang.Content = Loc.S(_lang, "langSwitch");
        BtnFullscreen.ToolTip = Loc.S(_lang, "fullscreen");
        BtnWinMin.ToolTip = Loc.S(_lang, "winMin");
        BtnWinMax.ToolTip = Loc.S(_lang, "winMax");
        BtnWinClose.ToolTip = Loc.S(_lang, "winClose");
        BtnBack.ToolTip = Loc.S(_lang, "navBack");
        BtnForward.ToolTip = Loc.S(_lang, "navForward");
        BtnHome.ToolTip = Loc.S(_lang, "navHome");
        UpdateReloadStop();
        if (_env is null) ShowAddressHint(Loc.S(_lang, "startingEngine"));
        else ShowAddressHint(Loc.S(_lang, "addressHint"));
    }

    /* ═════════════════ fatal-error panel (§5/§75 — never a dead window) ═════════════════ */

    private bool _errOpensRuntimeDownload;

    private void ShowFatal(string title, string detail, string actionText, bool openRuntimeDownload)
    {
        _errOpensRuntimeDownload = openRuntimeDownload;
        ErrTitle.Text = title;
        ErrDetail.Text = detail;
        BtnErrAction.Content = actionText;
        BtnErrClose.Content = Loc.S(_lang, "closeApp");
        FatalErrorPanel.Visibility = Visibility.Visible;
    }

    private void OnErrActionClick(object sender, RoutedEventArgs e)
    {
        if (_errOpensRuntimeDownload)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703", // official Evergreen installer
                    UseShellExecute = true,
                });
            }
            catch (Exception ex) { Logger.Error("could not open runtime download: " + ex.Message); }
        }
    }

    private void OnErrCloseClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
}
