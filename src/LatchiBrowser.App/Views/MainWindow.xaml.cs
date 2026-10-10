using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using LatchiBrowser.App.Browser;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Models;
using LatchiBrowser.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LatchiBrowser.App.Views;

/// <summary>
/// LATCHI Browser main window. Rounds 1-7 + this round: instant start page (the
/// engine warms up in the background — the UI NEVER freezes or shows engine state),
/// session restore with LAZY tab webviews, editable Quick Access, customizable
/// background, real tab context menu, optional Google accounts (never a gate).
/// Every button that exists here is wired to a real function (§107).
/// </summary>
public partial class MainWindow : Window
{
    // ── tabs, per profile (in-window account switching, §14/§19) ──
    private readonly Dictionary<string, ObservableCollection<BrowserTab>> _tabsByProfile = new();
    private readonly Dictionary<string, BrowserTab> _activeByProfile = new();
    private readonly Dictionary<string, HistoryStore> _historyByProfile = new();
    private ObservableCollection<BrowserTab> _tabs = new();     // bound: current profile's list
    private readonly List<string> _recentlyClosed = new();      // Ctrl+Shift+T (§11)
    private const int RecentlyClosedCap = 25;

    private SettingsStore _settings = new();
    private ProfileStore _profiles = null!;
    private BookmarkStore _bookmarks = null!;
    private ShortcutStore _shortcuts = null!;
    private SessionStore _session = null!;
    private string _lang = "ar";
    private string _currentProfileId = "";
    private readonly bool _isPrivateWindow;                     // §36 — window-wide InPrivate
    private readonly BrowserProfile? _startProfile;             // window may be opened for a profile
    private readonly bool _addGoogleOnStart;                    // welcome → "Add Google Account"
    private CoreWebView2Environment? _env;
    private bool _appFullscreen;        // F11 (browser fullscreen)
    private bool _contentFullscreen;    // video/website fullscreen requested by a page (§8)
    private WindowChrome? _normalChrome;
    private WindowState _stateBeforeFullscreen = WindowState.Normal;
    private bool _addressHintOn;

    // ── LATCHI AI (§54-§59) ──
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };
    private readonly List<(string Role, string Text)> _aiHistory = new();
    private bool _aiBusy;

    // ── single-instance child windows ──
    private DownloadsWindow? _downloadsWin;
    private HistoryWindow? _historyWin;
    private BookmarksWindow? _bookmarksWin;
    private ExtensionsWindow? _extensionsWin;

    public MainWindow() : this(null, false, false) { }

    /// <summary>Opened by the welcome screen's optional "Add Google Account".</summary>
    public MainWindow(bool addGoogleOnStart) : this(null, false, addGoogleOnStart) { }

    /// <summary>Opened by Ctrl+N (same profile) or Ctrl+Shift+N (InPrivate window, §36/§13).</summary>
    public MainWindow(BrowserProfile? profile, bool inPrivate, bool addGoogleOnStart = false)
    {
        _startProfile = profile;
        _isPrivateWindow = inPrivate;
        _addGoogleOnStart = addGoogleOnStart;
        InitializeComponent();
        TabsControl.ItemsSource = _tabs;
        // tunneling, handled-too: keys reach us even when the WebView has focus
        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKey), true);
        Closing += (_, _) => { SaveSession(); CleanupTabs(); };

        // start page wiring (search / shortcuts / customization)
        StartPage.SearchRequested += text =>
        {
            if (Active is { } t) GoTo(t, UrlHelper.ResolveAddress(text, SearchEngine, Home));
        };
        StartPage.NavigateRequested += url => { if (Active is { } t) GoTo(t, url); };
        StartPage.AddShortcutRequested += () => EditShortcut(null);
        StartPage.EditShortcutRequested += idOrRemove => EditShortcut(idOrRemove);
        StartPage.CustomizeRequested += kind => ApplyCustomization(kind);
    }

    private BrowserTab? Active => _activeByProfile.TryGetValue(_currentProfileId, out var t) ? t : null;
    private string Home => SettingsStore.NormalizeHomePage(_settings.Current.HomePage);
    private SearchEngines.Engine SearchEngine => SearchEngines.Resolve(_settings.Current.SearchEngineId);

    /* ═════════════════ startup — the UI is INSTANT, the engine warms hidden ═════════════════ */

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings = new SettingsStore();
        _lang = SettingsStore.NormalizeLanguage(_settings.Current.Language);
        _profiles = new ProfileStore(AppPaths.DataDir);
        _bookmarks = new BookmarkStore(AppPaths.DataDir);
        _shortcuts = new ShortcutStore(AppPaths.DataDir);
        _session = new SessionStore(AppPaths.DataDir);
        _profiles.EnsureDefault(Loc.S(_lang, "profileDefault"));
        BookmarkBarRow.Visibility = _settings.Current.ShowBookmarksBar ? Visibility.Visible : Visibility.Collapsed;

        // everything the user can see right now — before any engine work
        ApplyLanguage();
        RebuildBookmarkBar();
        StartPage.BindShortcuts(_shortcuts.All);
        ApplyStartPageBackground();
        StartPage.Visibility = Visibility.Visible;   // instant home, no freeze, no "engine" text
        Logger.Info("window up (start page shown before engine init)");

        // §5: runtime check BEFORE anything — with a clear message + official installer
        if (!BrowserEngine.IsRuntimeInstalled())
        {
            Logger.Error("WebView2 runtime missing — install panel shown");
            ShowFatal(Loc.S(_lang, "errRuntimeTitle"), Loc.S(_lang, "errRuntimeDetail"),
                Loc.S(_lang, "openDownload"), openRuntimeDownload: true);
            return;
        }
        Logger.Info("WebView2 runtime " + BrowserEngine.RuntimeVersion);

        // shared engine — warms up while the user already sees and can use the home page
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

        await RestoreOrOpenTabsAsync();

        if (_addGoogleOnStart)
            await AddProfileAsync(openGoogleSignIn: true);
    }

    /// <summary>Reopens the previous session (LAZY webviews — only the active tab
    /// really loads) or opens one fresh start tab.</summary>
    private async Task RestoreOrOpenTabsAsync()
    {
        SessionState? s = _settings.Current.RestoreTabsOnStartup ? _session.Load() : null;

        if (s is not null && s.TabsByProfile.Count > 0)
        {
            foreach (var (pid, urls) in s.TabsByProfile)
            {
                if (_profiles.Find(pid) is null) continue;      // profile was removed
                var list = new ObservableCollection<BrowserTab>();
                _tabsByProfile[pid] = list;
                foreach (var u in urls.Take(30))
                {
                    var profile = _profiles.Find(pid)!;
                    var tab = new BrowserTab(profile, _isPrivateWindow, _lang);
                    WireTab(tab);
                    tab.MarkLazy(u);                             // NO webview yet — light startup
                    list.Add(tab);
                }
            }
            var activePid = _profiles.Find(s.ActiveProfileId) is not null
                ? s.ActiveProfileId
                : _tabsByProfile.Keys.First();
            Logger.Info("session restored: " + _tabsByProfile.Sum(kv => kv.Value.Count) + " tabs (lazy)");
            await SwitchToProfileAsync(activePid);
            return;
        }

        var start = _startProfile is not null && _profiles.Find(_startProfile.ProfileId) is not null
            ? _startProfile
            : _profiles.All[0];
        await SwitchToProfileAsync(start.ProfileId);
    }

    private void SaveSession()
    {
        try
        {
            var s = new SessionState { ActiveProfileId = _currentProfileId };
            foreach (var (pid, list) in _tabsByProfile)
            {
                var urls = list
                    .Select(t => t.IsOnStartPage ? UrlHelper.StartUrl : (t.PendingUrl ?? t.Address))
                    .Where(u => !string.IsNullOrWhiteSpace(u) && u != "about:blank")
                    .ToList();
                if (urls.Count > 0) s.TabsByProfile[pid] = urls;
            }
            _session.Save(s);
        }
        catch (Exception ex) { Logger.Warn("session save failed: " + ex.Message); }
    }

    /* ═════════════════ profiles (§14-§20) — optional Google, never a gate ═════════════════ */

    private BrowserProfile CurrentProfile =>
        _profiles.Find(_currentProfileId) ?? _profiles.All[0];

    private HistoryStore GetHistory(string profileId)
    {
        if (!_historyByProfile.TryGetValue(profileId, out var store))
        {
            store = new HistoryStore(AppPaths.DataDir, profileId);
            _historyByProfile[profileId] = store;
        }
        return store;
    }

    /// <summary>Switches this window to a profile in place: tabs of other profiles stay
    /// alive (sessions persist!) and only the active profile's webview is visible.</summary>
    private async Task SwitchToProfileAsync(string profileId)
    {
        if (!_tabsByProfile.TryGetValue(profileId, out var list))
        {
            list = new ObservableCollection<BrowserTab>();
            _tabsByProfile[profileId] = list;
        }
        _currentProfileId = profileId;
        _profiles.Touch(profileId);
        _tabs = list;
        TabsControl.ItemsSource = _tabs;

        if (list.Count == 0)
        {
            await NewTabAsync();
            return;
        }
        ActivateTab(_activeByProfile.GetValueOrDefault(profileId) ?? list[0]);
    }

    private void OnProfileClick(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { FontSize = 12.5 };

        foreach (var p in _profiles.All)
        {
            var mi = new MenuItem
            {
                Header = p.DisplayName,
                IsChecked = p.ProfileId == _currentProfileId,
            };
            var id = p.ProfileId;
            mi.Click += async (_, _) =>
            {
                if (id != _currentProfileId) await SwitchToProfileAsync(id);
            };
            menu.Items.Add(mi);
        }

        menu.Items.Add(new Separator());

        var miAdd = new MenuItem { Header = Loc.S(_lang, "profileAdd") };
        miAdd.Click += async (_, _) => await AddProfileAsync();
        menu.Items.Add(miAdd);

        var miRename = new MenuItem { Header = Loc.S(_lang, "profileRename") };
        miRename.Click += (_, _) => RenameCurrentProfile();
        menu.Items.Add(miRename);

        // removing the last remaining profile makes no sense — disable honestly
        var miRemove = new MenuItem
        {
            Header = Loc.S(_lang, "profileRemove"),
            IsEnabled = _profiles.All.Count > 1,
        };
        miRemove.Click += (_, _) => RemoveCurrentProfile();
        menu.Items.Add(miRemove);

        menu.PlacementTarget = BtnProfile;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private async Task AddProfileAsync(bool openGoogleSignIn = false)
    {
        var name = PromptWindow.Show(this, Loc.S(_lang, "profileAdd"),
            Loc.S(_lang, "profileAddNote"),
            ProfileStore.NormalizeDisplayName(null, _profiles.All.Count + 1, _lang), _lang);
        if (string.IsNullOrWhiteSpace(name)) return;

        var p = _profiles.Add(name);
        Logger.Info("profile added: " + p.DisplayName);
        await SwitchToProfileAsync(p.ProfileId);
        // the real Google sign-in page — LATCHI never asks for or sees any password
        await NewTabAsync(openGoogleSignIn ? "https://accounts.google.com/" : UrlHelper.StartUrl);
    }

    private void RenameCurrentProfile()
    {
        var current = CurrentProfile;
        var name = PromptWindow.Show(this, Loc.S(_lang, "profileRename"), "",
            current.DisplayName, _lang);
        if (string.IsNullOrWhiteSpace(name)) return;
        _profiles.Rename(current.ProfileId, name);
    }

    private async void RemoveCurrentProfile()
    {
        var p = CurrentProfile;
        if (MessageBox.Show(this, Loc.S(_lang, "profileRemoveConfirm"), Loc.S(_lang, "profileRemove"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        if (_tabsByProfile.TryGetValue(p.ProfileId, out var list))
        {
            foreach (var t in list) t.Dispose();
            foreach (var t in list) if (t.WebView is not null) ContentHost.Children.Remove(t.WebView);
            list.Clear();
            _tabsByProfile.Remove(p.ProfileId);
        }
        _activeByProfile.Remove(p.ProfileId);
        try
        {
            var histDir = Path.Combine(AppPaths.DataDir, "Profiles", p.ProfileId);
            if (Directory.Exists(histDir)) Directory.Delete(histDir, recursive: true);
        }
        catch (Exception ex) { Logger.Warn("history dir cleanup failed: " + ex.Message); }

        _profiles.Remove(p.ProfileId);
        Logger.Info("profile removed: " + p.DisplayName);

        var next = _tabsByProfile.Where(kv => kv.Value.Count > 0)
            .OrderByDescending(kv => _profiles.Find(kv.Key)?.LastUsedUtc ?? DateTime.MinValue)
            .Select(kv => kv.Key)
            .FirstOrDefault() ?? _profiles.All[0].ProfileId;
        await SwitchToProfileAsync(next);
    }

    /* ═════════════════ tabs (§11/§12 + context menu + lazy restore) ═════════════════ */

    private void WireTab(BrowserTab tab)
    {
        // popup → new tab (§47). NOTE: parameter is 't' — a '_' here would capture as
        // BrowserTab inside the inner lambda and break the discard (real compiler trap).
        tab.PopupRequested += (t, uri) => Dispatcher.Invoke(() => _ = NewTabAsync(uri));
        tab.ContentFullscreenChanged += (_, on) => Dispatcher.Invoke(() =>
        {
            _contentFullscreen = on;
            if (on) EnterFullscreen(hideChrome: false);
            else ExitFullscreenIfIdle();
        });
        tab.NavigationCommitted += (t, title, address) => Dispatcher.Invoke(() =>
            GetHistory(t.ProfileId).Add(title, address));   // §26 — private tabs never raise it
        tab.PropertyChanged += (s, e) => Dispatcher.Invoke(() =>
            OnTabPropertyChanged((BrowserTab)s!, e.PropertyName!));
    }

    private async Task NewTabAsync(string? url = null)
    {
        if (_env is null) return; // engine not ready yet — button is honest but inert

        var tab = new BrowserTab(CurrentProfile, _isPrivateWindow, _lang);
        WireTab(tab);
        _tabs.Add(tab);
        var start = url ?? Home;
        await tab.InitializeAsync(_env, start);
        if (tab.WebView is not null) ContentHost.Children.Add(tab.WebView);
        ActivateTab(tab);
    }

    /// <summary>Creates the webview of a lazy (restored) tab when it becomes active.</summary>
    private async Task EnsureTabInitializedAsync(BrowserTab tab)
    {
        if (tab.IsInitialized || _env is null) return;
        try
        {
            var url = tab.PendingUrl ?? UrlHelper.StartUrl;
            await tab.InitializeAsync(_env, url);
            if (tab.WebView is not null && !ContentHost.Children.Contains(tab.WebView))
                ContentHost.Children.Add(tab.WebView);
            UpdateChromeForActiveTab();
        }
        catch (Exception ex)
        {
            Logger.Error("lazy tab init failed: " + ex.Message);
        }
    }

    private void ActivateTab(BrowserTab tab)
    {
        _activeByProfile[tab.ProfileId] = tab;
        tab.Touch();
        foreach (var t in _tabs) t.IsActive = t == tab;
        UpdateChromeForActiveTab();
        // lazy session-restore tab: create its webview now (async, keeps UI responsive)
        if (!tab.IsInitialized && _env is not null)
            _ = EnsureTabInitializedAsync(tab);
        SyncToolbar();
    }

    /// <summary>Single place that decides what covers the content area: the active
    /// tab's webview, or the LATCHI start page (when no tab / start-page tab).</summary>
    private void UpdateChromeForActiveTab()
    {
        var tab = Active;
        var showStart = tab is null || tab.IsOnStartPage;
        StartPage.Visibility = showStart ? Visibility.Visible : Visibility.Collapsed;
        foreach (var child in ContentHost.Children.OfType<WebView2>())
            child.Visibility = !showStart && child == tab!.WebView
                ? Visibility.Visible : Visibility.Collapsed;
        if (showStart)
        {
            StartPage.BindRows(_bookmarks.All, GetHistory(_currentProfileId).All);
            StartPage.BindShortcuts(_shortcuts.All);
        }
    }

    private void CloseTab(BrowserTab tab)
    {
        var idx = _tabs.IndexOf(tab);
        if (idx < 0) return;

        // remember the address for Ctrl+Shift+T (only real pages)
        var address = tab.PendingUrl ?? tab.Address;
        if (Uri.TryCreate(address, UriKind.Absolute, out var u)
            && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
        {
            _recentlyClosed.Add(u.ToString());
            while (_recentlyClosed.Count > RecentlyClosedCap) _recentlyClosed.RemoveAt(0);
        }

        _tabs.RemoveAt(idx);
        if (tab.WebView is not null) ContentHost.Children.Remove(tab.WebView);
        tab.Dispose();
        if (_activeByProfile.GetValueOrDefault(tab.ProfileId) == tab)
            _activeByProfile.Remove(tab.ProfileId);

        if (_tabs.Count == 0)
        {
            // other profiles still have live tabs here? → switch to the last-used one.
            // otherwise the last tab closed the window, like every real browser.
            var next = _tabsByProfile.Where(kv => kv.Key != tab.ProfileId && kv.Value.Count > 0)
                .OrderByDescending(kv => _profiles.Find(kv.Key)?.LastUsedUtc ?? DateTime.MinValue)
                .Select(kv => kv.Key).FirstOrDefault();
            if (next is not null) _ = SwitchToProfileAsync(next);
            else Close();
            return;
        }
        if (Active == null) ActivateTab(_tabs[Math.Min(idx, _tabs.Count - 1)]);
        else UpdateChromeForActiveTab();
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
        if (_tabs.Count < 2 || Active is null) return;
        var i = _tabs.IndexOf(Active);
        ActivateTab(_tabs[(i + delta + _tabs.Count) % _tabs.Count]);
    }

    private void CleanupTabs()
    {
        foreach (var list in _tabsByProfile.Values)
            foreach (var t in list) t.Dispose();
        _tabsByProfile.Clear();
    }

    // ── tab context menu: duplicate / close / close others / close right / reopen ──

    private void OnTabRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: BrowserTab t }) return;
        e.Handled = true;

        var menu = new ContextMenu { FontSize = 12.5 };
        var idx = _tabs.IndexOf(t);

        var dup = new MenuItem { Header = Loc.S(_lang, "duplicateTab") };
        dup.Click += async (_, _) =>
            await NewTabAsync(t.IsOnStartPage ? null : (t.PendingUrl ?? t.Address));
        menu.Items.Add(dup);

        var reopen = new MenuItem
        {
            Header = Loc.S(_lang, "reopenTab"),
            IsEnabled = _recentlyClosed.Count > 0,
        };
        reopen.Click += (_, _) => ReopenClosedTab();
        menu.Items.Add(reopen);

        menu.Items.Add(new Separator());

        var close = new MenuItem { Header = Loc.S(_lang, "tabClose") };
        close.Click += (_, _) => CloseTab(t);
        menu.Items.Add(close);

        var others = new MenuItem
        {
            Header = Loc.S(_lang, "closeOtherTabs"),
            IsEnabled = _tabs.Count > 1,
        };
        others.Click += (_, _) =>
        {
            foreach (var x in _tabs.Where(x => x != t).ToList()) CloseTab(x);
            ActivateTab(t);
        };
        menu.Items.Add(others);

        var right = new MenuItem
        {
            Header = Loc.S(_lang, "closeTabsRight"),
            IsEnabled = idx >= 0 && idx < _tabs.Count - 1,
        };
        right.Click += (_, _) =>
        {
            for (var i = _tabs.Count - 1; i > idx; i--) CloseTab(_tabs[i]);
            ActivateTab(t);
        };
        menu.Items.Add(right);

        menu.PlacementTarget = (UIElement)sender;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    /* ═════════════════ toolbar ↔ active tab ═════════════════ */

    private void OnTabPropertyChanged(BrowserTab tab, string prop)
    {
        if (tab != Active) return;
        switch (prop)
        {
            case nameof(BrowserTab.Address): UpdateAddressBox(); break;
            case nameof(BrowserTab.CanGoBack): BtnBack.IsEnabled = tab.CanGoBack; break;
            case nameof(BrowserTab.CanGoForward): BtnForward.IsEnabled = tab.CanGoForward; break;
            case nameof(BrowserTab.IsLoading): UpdateReloadStop(); break;
            case nameof(BrowserTab.Title): SyncStar(); break;
        }
    }

    private void SyncToolbar()
    {
        BtnBack.IsEnabled = Active?.CanGoBack == true;
        BtnForward.IsEnabled = Active?.CanGoForward == true;
        BtnStar.IsEnabled = Active is not null && !Active.IsOnStartPage; // start page isn't bookmarkable
        UpdateReloadStop();
        UpdateAddressBox();
        SyncStar();
    }

    private void UpdateReloadStop()
    {
        var loading = Active?.IsLoading == true;
        BtnReloadStop.Content = loading ? "\uE71A" : "\uE72C"; // Cancel : Refresh
        BtnReloadStop.ToolTip = Loc.S(_lang, loading ? "navStop" : "navReload");
    }

    private void UpdateAddressBox()
    {
        if (Active is null) return;
        if (Active.IsOnStartPage)
        {
            // start page: empty bar + hint — never an internal pseudo-URL
            if (!AddressBox.IsKeyboardFocused)
                ShowAddressHint(Loc.S(_lang, "addressHint"));
            return;
        }
        if (!AddressBox.IsKeyboardFocused && !_addressHintOn)
            AddressBox.Text = Active.Address;
        var secure = Active.Address.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        LockGlyph.Text = secure ? "\uE72E" : "\uE7BA";
        LockGlyph.Foreground = secure
            ? (Brush)FindResource("BrushMuted")
            : (Brush)FindResource("BrushDanger");
    }

    private void SyncStar()
    {
        var bookmarked = Active is not null && _bookmarks is not null && _bookmarks.Contains(Active.Address);
        BtnStar.Content = bookmarked ? "\uE735" : "\uE734"; // ★ : ☆ (Segoe MDL2, §84)
        BtnStar.ToolTip = Loc.S(_lang, bookmarked ? "starRemove" : "starAdd");
    }

    /* ═════════════════ navigation (§22) — one door, start-page aware ═════════════════ */

    private void GoTo(BrowserTab tab, string url)
    {
        tab.Navigate(url);                       // handles latchi://start itself
        UpdateChromeForActiveTab();
        SyncToolbar();
    }

    private void OnStartPageSearch(string text)
    {
        if (Active is null) return;
        GoTo(Active, UrlHelper.ResolveAddress(text, SearchEngine, Home));
    }

    private void OnStartPageNavigate(string url)
    {
        if (Active is null) return;
        GoTo(Active, url);
    }

    private void OnAddressKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _addressHintOn = false;
        AddressBox.Foreground = (Brush)FindResource("BrushText");
        var resolved = UrlHelper.ResolveAddress(AddressBox.Text, SearchEngine, Home);
        if (Active is not null)
        {
            GoTo(Active, resolved);
            Active.WebView?.Focus(); // keys belong to the page again
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
        AddressBox.Foreground = (Brush)FindResource("BrushMuted");
    }

    private void ClearAddressHint()
    {
        if (!_addressHintOn) return;
        _addressHintOn = false;
        AddressBox.Text = "";
        AddressBox.Foreground = (Brush)FindResource("BrushText");
    }

    /* ═════════════════ bookmarks (§24/§25) ═════════════════ */

    private void OnStarClick(object sender, RoutedEventArgs e) => ToggleBookmark();

    private void ToggleBookmark()
    {
        if (Active is null || _bookmarks is null) return;
        var addr = Active.Address;
        if (!addr.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !addr.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
        var added = _bookmarks.Toggle(Active.Title, addr);
        Logger.Info(added ? "bookmark added: " + Logger.HostOnly(addr) : "bookmark removed");
        RebuildBookmarkBar();
        SyncStar();
    }

    private void RebuildBookmarkBar()
    {
        if (BookmarkBarHost is null || _bookmarks is null) return;
        BookmarkBarHost.Children.Clear();

        if (_bookmarks.All.Count == 0)
        {
            BookmarkBarHost.Children.Add(new TextBlock
            {
                Text = Loc.S(_lang, "bookmarksBarEmpty"),
                Style = (Style)FindResource("BodyText"),
                FontSize = 11.5,
                Foreground = (Brush)FindResource("BrushMuted"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            return;
        }

        foreach (var b in _bookmarks.All)
        {
            var url = b.Url;
            var chip = new Button
            {
                Style = (Style)FindResource("BookmarkChip"),
                Content = b.Title,
                ToolTip = b.Url,
            };
            chip.Click += (_, _) => { if (Active is not null) GoTo(Active, url); };
            chip.ContextMenu = new ContextMenu { FontSize = 12.5 };
            var miOpen = new MenuItem { Header = Loc.S(_lang, "bookmarkOpen") };
            miOpen.Click += (_, _) => { if (Active is not null) GoTo(Active, url); };
            var miRemove = new MenuItem { Header = Loc.S(_lang, "bookmarkRemove") };
            miRemove.Click += (_, _) => { _bookmarks.Remove(url); RebuildBookmarkBar(); SyncStar(); };
            chip.ContextMenu.Items.Add(miOpen);
            chip.ContextMenu.Items.Add(miRemove);
            chip.MouseRightButtonUp += (_, e2) => { chip.ContextMenu.PlacementTarget = chip; chip.ContextMenu.IsOpen = true; e2.Handled = true; };
            BookmarkBarHost.Children.Add(chip);
        }
    }

    private void ToggleBookmarksBar()
    {
        var visible = BookmarkBarRow.Visibility != Visibility.Visible;
        BookmarkBarRow.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        _settings.Current.ShowBookmarksBar = visible;
        _settings.Save();
    }

    /* ═════════════════ start page: editable shortcuts + background ═════════════════ */

    private void EditShortcut(string? idOrRemove)
    {
        if (idOrRemove is not null && idOrRemove.StartsWith("remove:"))
        {
            _shortcuts.Remove(idOrRemove["remove:".Length..]);
            StartPage.BindShortcuts(_shortcuts.All);
            return;
        }
        var existing = idOrRemove is null
            ? null
            : _shortcuts.All.FirstOrDefault(s => s.Id == idOrRemove);
        var res = ShortcutWindow.Show(this,
            existing is null ? Loc.S(_lang, "addShortcut") : Loc.S(_lang, "editShortcut"),
            existing?.Title ?? "", existing?.Url ?? "https://", _lang);
        if (res is null) return;
        if (existing is null) _shortcuts.Add(res.Value.Name, res.Value.Url);
        else _shortcuts.Update(existing.Id, res.Value.Name, res.Value.Url);
        StartPage.BindShortcuts(_shortcuts.All);
    }

    /// <summary>From the start page's customize menu: dark/color/image/remove.</summary>
    private void ApplyCustomization(string kind) => ApplyBackground(kind);

    /// <summary>From Settings (color swatches send '#…' directly).</summary>
    private void ApplyCustomizeFromSettings(string kind) => ApplyBackground(kind);

    private void ApplyBackground(string kind)
    {
        if (kind.StartsWith('#'))
        {
            _settings.Current.StartPageBackground = kind;
            _settings.Save();
            ApplyStartPageBackground();
            return;
        }
        switch (kind)
        {
            case "dark":
            case "remove":
                _settings.Current.StartPageBackground = "";
                _settings.Save();
                ApplyStartPageBackground();
                break;

            case "color":
                var menu = new ContextMenu { FontSize = 12.5 };
                foreach (var (name, hex) in new[]
                {
                    (Loc.S(_lang, "bgNavy"), "#FF0B1E3D"),
                    (Loc.S(_lang, "bgViolet"), "#FF2A1B3D"),
                    (Loc.S(_lang, "bgGreen"), "#FF0F2B22"),
                    (Loc.S(_lang, "bgGray"), "#FF20242B"),
                    (Loc.S(_lang, "bgWine"), "#FF331422"),
                })
                {
                    var hexCopy = hex;
                    var mi = new MenuItem { Header = name };
                    mi.Click += (_, _) =>
                    {
                        _settings.Current.StartPageBackground = hexCopy;
                        _settings.Save();
                        ApplyStartPageBackground();
                    };
                    menu.Items.Add(mi);
                }
                menu.PlacementTarget = StartPage;
                menu.Placement = PlacementMode.MousePoint;
                menu.IsOpen = true;
                break;

            case "image":
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = Loc.S(_lang, "bgImage"),
                    Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                };
                if (dlg.ShowDialog(this) != true) return;
                try
                {
                    // copy into the data dir — light, self-contained, survives restarts
                    var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    if (ext == ".jpeg") ext = ".jpg";
                    var dest = Path.Combine(AppPaths.DataDir, "startpage-bg" + ext);
                    foreach (var old in Directory.GetFiles(AppPaths.DataDir, "startpage-bg.*"))
                        File.Delete(old);
                    File.Copy(dlg.FileName, dest, overwrite: true);
                    _settings.Current.StartPageBackground = "startpage-bg" + ext;
                    _settings.Save();
                    ApplyStartPageBackground();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, Loc.S(_lang, "appName"));
                }
                break;
        }
    }

    private void ApplyStartPageBackground()
    {
        var bg = _settings.Current.StartPageBackground;
        try
        {
            if (string.IsNullOrWhiteSpace(bg) || bg == "dark")
                StartPage.Background = (Brush)FindResource("BrushContentBg");
            else if (bg.StartsWith('#'))
                StartPage.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));
            else
            {
                var path = Path.Combine(AppPaths.DataDir, bg);
                if (File.Exists(path))
                {
                    var img = new BitmapImage(new Uri(path));
                    StartPage.Background = new ImageBrush(img) { Stretch = Stretch.UniformToFill };
                }
                else StartPage.Background = (Brush)FindResource("BrushContentBg");
            }
        }
        catch
        {
            StartPage.Background = (Brush)FindResource("BrushContentBg");
        }
    }

    /* ═════════════════ LATCHI AI sidebar (§54-§59) — honest, never fake ═════════════════ */

    private void OnAiToggleClick(object sender, RoutedEventArgs e) => ToggleAi();
    private void OnAiCloseClick(object sender, RoutedEventArgs e) => ToggleAi();

    private void ToggleAi()
    {
        var show = AiPanel.Visibility != Visibility.Visible;
        AiPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            if (AiMessages.Children.Count == 0)
            {
                if (!_settings.Current.GeminiEnabled)
                    AddAiMessage("system", Loc.S(_lang, "aiDisabled"));
                else if (!Services.SecureKeyStore.HasKey())
                    AddAiMessage("system", Loc.S(_lang, "aiNoKey"));
                else
                    AddAiMessage("assistant", Loc.S(_lang, "aiGreeting"));
            }
            AiInput.Focus();
        }
    }

    private void AddAiMessage(string role, string text)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(4, 4, 4, 4),
            MaxWidth = 300,
            HorizontalAlignment = role == "user" ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
        if (role == "user")
        {
            border.Background = new SolidColorBrush(Color.FromArgb(0x4D, 0xE3, 0xB3, 0x41)); // translucent gold
        }
        else if (role == "assistant")
        {
            border.Background = (Brush)FindResource("BrushField");
            border.BorderBrush = (Brush)FindResource("BrushBorder");
            border.BorderThickness = new Thickness(1);
        }
        else // system / error
        {
            border.Background = (Brush)FindResource("BrushHover");
        }

        var tb = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = (Brush)FindResource("BrushText"),
            LineHeight = 19,
        };
        border.Child = tb;
        AiMessages.Children.Add(border);
        // let the new bubble lay out, then scroll to it (explicit classic overload —
        // the (Action, priority) shape can silently bind to params object[] instead)
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            (Action)(() => AiScroll.ScrollToEnd()));
    }

    private void OnAiInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _ = SendAiAsync();
            e.Handled = true;
        }
    }

    private void OnAiSendClick(object sender, RoutedEventArgs e) => _ = SendAiAsync();

    private async Task SendAiAsync()
    {
        var text = AiInput.Text.Trim();
        if (text.Length == 0 || _aiBusy) return;

        if (!_settings.Current.GeminiEnabled)
        {
            AddAiMessage("system", Loc.S(_lang, "aiDisabled"));
            return;
        }
        var key = Services.SecureKeyStore.LoadKey();
        if (key is null)
        {
            AddAiMessage("system", Loc.S(_lang, "aiNoKey"));
            return;
        }

        AiInput.Clear();
        AddAiMessage("user", text);
        _aiBusy = true;
        BtnAiSend.IsEnabled = false;
        AiStatus.Text = Loc.S(_lang, "aiThinking");
        AiStatus.Visibility = Visibility.Visible;

        try
        {
            var body = Gemini.BuildRequestBody(text, _aiHistory);
            using var req = new HttpRequestMessage(HttpMethod.Post,
                Gemini.Endpoint(_settings.Current.GeminiModel));
            req.Headers.Add("x-goog-api-key", key);   // key travels to Google only (§56)
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
            using var resp = await Http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            var answer = Gemini.ParseResponse(json);  // throws a clear message on API errors

            _aiHistory.Add(("user", text));
            _aiHistory.Add(("model", answer));
            if (_aiHistory.Count > 40) _aiHistory.RemoveRange(0, _aiHistory.Count - 40);

            AddAiMessage("assistant", answer);
        }
        catch (Exception ex)
        {
            AddAiMessage("error", Loc.S(_lang, "aiErrorPrefix") + ex.Message);
        }
        finally
        {
            _aiBusy = false;
            BtnAiSend.IsEnabled = true;
            AiStatus.Visibility = Visibility.Collapsed;
        }
    }

    /* ═════════════════ zoom (§38) / DevTools (§40) / memory (§63) ═════════════════ */

    private void ZoomBy(double delta)
    {
        // ZoomFactor lives on the WPF control (wraps CoreWebView2Controller.ZoomFactor)
        if (Active?.WebView is not { } wv) return;
        wv.ZoomFactor = Math.Clamp(wv.ZoomFactor + delta, 0.25, 5.0);
    }

    private void ZoomReset()
    {
        if (Active?.WebView is not { } wv) return;
        wv.ZoomFactor = 1.0;
    }

    private void OpenDevTools() => Active?.Core?.OpenDevToolsWindow();

    /// <summary>§63 honest subset: WPF's WebView2 exposes no controller-level
    /// TrySuspend, so we use the supported MemoryUsageTargetLevel signal instead.</summary>
    private void ApplyMemoryTarget(bool minimized)
    {
        var level = minimized
            ? CoreWebView2MemoryUsageTargetLevel.Low
            : CoreWebView2MemoryUsageTargetLevel.Normal;
        foreach (var list in _tabsByProfile.Values)
            foreach (var t in list)
            {
                try { if (t.Core is not null) t.Core.MemoryUsageTargetLevel = level; }
                catch { /* per-tab best effort */ }
            }
    }

    /* ═════════════════ privacy: real clearing via WebView2 ═════════════════ */

    private async Task ClearBrowsingDataAsync(CoreWebView2BrowsingDataKinds kinds, string whatLabel)
    {
        var profile = Active?.Core?.Profile;
        if (profile is null)
        {
            MessageBox.Show(this, Loc.S(_lang, "privacyNeedTab"), Loc.S(_lang, "privacyTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            await profile.ClearBrowsingDataAsync(kinds);
            Logger.Info("cleared: " + whatLabel);
            MessageBox.Show(this, Loc.S(_lang, "privacyDone"), Loc.S(_lang, "privacyTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Loc.S(_lang, "privacyTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /* ═════════════════ child windows ═════════════════ */

    private void OnDownloadsClick(object sender, RoutedEventArgs e) => ShowDownloads();

    private void ShowDownloads()
    {
        if (_downloadsWin is not null && _downloadsWin.IsLoaded)
        {
            _downloadsWin.Activate();
            return;
        }
        _downloadsWin = new DownloadsWindow(this, _lang) { Owner = this };
        _downloadsWin.Show();
    }

    private void ShowHistory()
    {
        if (_historyWin is not null && _historyWin.IsLoaded)
        {
            _historyWin.Activate();
            return;
        }
        _historyWin = new HistoryWindow(this, GetHistory(_currentProfileId),
            url => NewTabAsync(url), _lang) { Owner = this };
        _historyWin.Show();
    }

    private void ShowBookmarks()
    {
        if (_bookmarksWin is not null && _bookmarksWin.IsLoaded)
        {
            _bookmarksWin.Activate();
            return;
        }
        _bookmarksWin = new BookmarksWindow(this, _bookmarks,
            url => NewTabAsync(url), _lang) { Owner = this };
        _bookmarksWin.Show();
    }

    private void ShowExtensions()
    {
        if (_extensionsWin is not null && _extensionsWin.IsLoaded)
        {
            _extensionsWin.Activate();
            return;
        }
        if (Active?.Core is null) return; // nothing initialized yet — honest no-op
        _extensionsWin = new ExtensionsWindow(this, Active, _lang) { Owner = this };
        _extensionsWin.Show();
    }

    private void ShowSettings()
    {
        var w = new SettingsWindow(this, _settings, OnSettingsSaved, _lang,
            ApplyCustomizeFromSettings,
            () => _ = ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage,
                "cache"),
            () => _ = ClearBrowsingDataAsync(
                CoreWebView2BrowsingDataKinds.Cookies | CoreWebView2BrowsingDataKinds.AllDomStorage,
                "cookies"),
            ClearHistoryNow)
        { Owner = this };
        w.ShowDialog();
    }

    private void ClearHistoryNow()
    {
        GetHistory(_currentProfileId).ClearAll();
        UpdateChromeForActiveTab(); // start page's "recently visited" refreshes
        MessageBox.Show(this, Loc.S(_lang, "privacyDone"), Loc.S(_lang, "privacyTitle"),
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnSettingsSaved()
    {
        var newLang = SettingsStore.NormalizeLanguage(_settings.Current.Language);
        var langChanged = newLang != _lang;
        _lang = newLang;
        ApplyLanguage();
        RebuildBookmarkBar();
        ApplyStartPageBackground();
        StartPage.BindShortcuts(_shortcuts.All);
        UpdateChromeForActiveTab();
        if (langChanged) AiModel.Text = _settings.Current.GeminiModel;
        SyncStar();
        Logger.Info("settings saved");
    }

    /* ═════════════════ click handlers — every one is a real function (§107) ═════════════════ */

    private void OnBackClick(object sender, RoutedEventArgs e) => Active?.GoBack();
    private void OnForwardClick(object sender, RoutedEventArgs e) => Active?.GoForward();

    private void OnReloadStopClick(object sender, RoutedEventArgs e)
    {
        if (Active is null) return;
        if (Active.IsLoading) Active.Stop();
        else if (!Active.IsOnStartPage) Active.Reload(); // nothing to reload on the start page
    }

    private void OnHomeClick(object sender, RoutedEventArgs e)
    {
        if (Active is null) return;
        GoTo(Active, Home);   // Home may be the internal start page — GoTo handles it
    }

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

        MenuItem Mi(string header, RoutedEventHandler onClick, bool enabled = true)
        {
            var mi = new MenuItem { Header = header, IsEnabled = enabled };
            mi.Click += onClick;
            return mi;
        }

        menu.Items.Add(Mi(Loc.S(_lang, "newTabBtn"), async (_, _) => await NewTabAsync()));
        menu.Items.Add(Mi(Loc.S(_lang, "newWindow"), (_, _) =>
            new MainWindow(CurrentProfile, false).Show()));
        menu.Items.Add(Mi(Loc.S(_lang, "newPrivateWindow"), (_, _) =>
            new MainWindow(CurrentProfile, true).Show()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Mi(Loc.S(_lang, "menuBar"), (_, _) => ToggleBookmarksBar()));
        menu.Items.Add(Mi(Loc.S(_lang, "menuBookmarks"), (_, _) => ShowBookmarks()));
        menu.Items.Add(Mi(Loc.S(_lang, "menuHistory"), (_, _) => ShowHistory()));
        menu.Items.Add(Mi(Loc.S(_lang, "menuDownloads"), (_, _) => ShowDownloads()));
        menu.Items.Add(Mi(Loc.S(_lang, "menuExtensions"), (_, _) => ShowExtensions(),
            enabled: Active?.Core is not null));
        menu.Items.Add(new Separator());
        menu.Items.Add(Mi(Loc.S(_lang, "menuZoomIn"), (_, _) => ZoomBy(+0.1)));
        menu.Items.Add(Mi(Loc.S(_lang, "menuZoomOut"), (_, _) => ZoomBy(-0.1)));
        menu.Items.Add(Mi(Loc.S(_lang, "menuZoomReset"), (_, _) => ZoomReset()));
        menu.Items.Add(new Separator());
        menu.Items.Add(Mi(Loc.S(_lang, "menuAi"), (_, _) => ToggleAi()));
        menu.Items.Add(Mi(Loc.S(_lang, "langSwitch"), (_, _) => ToggleLanguage()));
        menu.Items.Add(Mi(Loc.S(_lang, "reopenTab"), (_, _) => ReopenClosedTab(),
            enabled: _recentlyClosed.Count > 0));
        menu.Items.Add(Mi(Loc.S(_lang, "menuSettings"), (_, _) => ShowSettings()));
        menu.Items.Add(Mi(Loc.S(_lang, "fullscreen"), (_, _) => ToggleAppFullscreen()));
        menu.Items.Add(Mi(Loc.S(_lang, "menuAbout"), (_, _) => ShowAbout()));
        menu.Items.Add(Mi(Loc.S(_lang, "menuExit"), (_, _) => Close()));

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

    private void OnWindowStateChanged(object sender, EventArgs e)
    {
        BtnWinMax.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922"; // Restore : Maximize
        // §63: when minimized, tell Chromium it may shed memory; restore on return
        ApplyMemoryTarget(WindowState == WindowState.Minimized);
    }

    private void OnToolbarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { /* drag races are harmless */ }
        }
    }

    /* ═════════════════ fullscreen — three distinct cases (§8) ═════════════════ */

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

    /* ═════════════════ keyboard (§11 + all shortcuts) ═════════════════ */

    private void OnPreviewKey(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var alt = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

        if (e.Key == Key.F11) { ToggleAppFullscreen(); e.Handled = true; return; }
        if (e.Key == Key.Escape && _appFullscreen && !_contentFullscreen) { ToggleAppFullscreen(); e.Handled = true; return; }
        if (e.Key == Key.F12 || (ctrl && shift && e.Key == Key.I)) { OpenDevTools(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.T && !shift) { _ = NewTabAsync(); e.Handled = true; return; }
        if (ctrl && shift && e.Key == Key.T) { ReopenClosedTab(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.W) { if (Active is not null) CloseTab(Active); e.Handled = true; return; }
        if (ctrl && e.Key == Key.Tab) { CycleTab(shift ? -1 : +1); e.Handled = true; return; }
        if (ctrl && e.Key == Key.L) { AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.R) { if (Active is { IsOnStartPage: false }) Active.Reload(); e.Handled = true; return; }
        if (e.Key == Key.F5) { if (Active is { IsOnStartPage: false }) Active.Reload(); e.Handled = true; return; }
        if (alt && e.Key == Key.Left) { Active?.GoBack(); e.Handled = true; return; }
        if (alt && e.Key == Key.Right) { Active?.GoForward(); e.Handled = true; return; }
        if (ctrl && shift && e.Key == Key.B) { ToggleBookmarksBar(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.D && !shift) { ToggleBookmark(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.H && !shift) { ShowHistory(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.J && !shift) { ShowDownloads(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.N && !shift) { new MainWindow(CurrentProfile, false).Show(); e.Handled = true; return; }
        if (ctrl && shift && e.Key == Key.N) { new MainWindow(CurrentProfile, true).Show(); e.Handled = true; return; }
        if (ctrl && (e.Key == Key.OemPlus || e.Key == Key.Add)) { ZoomBy(+0.1); e.Handled = true; return; }
        if (ctrl && (e.Key == Key.OemMinus || e.Key == Key.Subtract)) { ZoomBy(-0.1); e.Handled = true; return; }
        if (ctrl && (e.Key == Key.D0 || e.Key == Key.NumPad0)) { ZoomReset(); e.Handled = true; return; }
    }

    /* ═════════════════ language (§77/§78: UI language only) ═════════════════ */

    private void OnToggleLanguage(object sender, RoutedEventArgs e) => ToggleLanguage();

    private void ToggleLanguage()
    {
        _lang = _lang == "ar" ? "en" : "ar";
        _settings.Current.Language = _lang;
        _settings.Save();
        ApplyLanguage();
        RebuildBookmarkBar();
        StartPage.ApplyLanguage(_lang);
        StartPage.BindShortcuts(_shortcuts.All);
        UpdateChromeForActiveTab();
        Logger.Info("UI language → " + _lang);
    }

    private void ApplyLanguage()
    {
        FlowDirection = _lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = Loc.S(_lang, "appName") + (_isPrivateWindow ? Loc.S(_lang, "privateSuffix") : "");
        BtnNewTab.ToolTip = Loc.S(_lang, "newTabBtn");
        BtnLang.Content = Loc.S(_lang, "langSwitch");
        BtnFullscreen.ToolTip = Loc.S(_lang, "fullscreen");
        BtnWinMin.ToolTip = Loc.S(_lang, "winMin");
        BtnWinMax.ToolTip = Loc.S(_lang, "winMax");
        BtnWinClose.ToolTip = Loc.S(_lang, "winClose");
        BtnBack.ToolTip = Loc.S(_lang, "navBack");
        BtnForward.ToolTip = Loc.S(_lang, "navForward");
        BtnHome.ToolTip = Loc.S(_lang, "navHome");
        BtnProfile.ToolTip = Loc.S(_lang, "profileBtn");
        BtnAi.ToolTip = Loc.S(_lang, "menuAi");
        BtnDownloads.ToolTip = Loc.S(_lang, "downloadsTitle");
        AiTitle.Text = Loc.S(_lang, "aiTitle");
        AiModel.Text = _settings?.Current.GeminiModel ?? Gemini.DefaultModel;
        AiInput.ToolTip = Loc.S(_lang, "aiInputHint");
        BtnAiSend.ToolTip = Loc.S(_lang, "aiSend");
        StartPage.ApplyLanguage(_lang);
        UpdateReloadStop();
        // the engine stays invisible (user request): no "starting" state is ever shown
        if (!AddressBox.IsKeyboardFocused) ShowAddressHint(Loc.S(_lang, "addressHint"));
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
