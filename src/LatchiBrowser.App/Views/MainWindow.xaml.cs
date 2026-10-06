using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shell;
using LatchiBrowser.App.Browser;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Models;
using LatchiBrowser.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LatchiBrowser.App.Views;

/// <summary>
/// LATCHI Browser main window — rounds 1-7 combined. Custom chrome (tab strip IS the
/// caption), real tabs, real per-profile isolation (§14-§19), bookmarks bar (§25),
/// history (§26), tracked downloads (§27), extensions (§28), InPrivate windows (§36),
/// zoom (§38), DevTools (§40), multi-window (§13) and the honest Gemini-backed
/// LATCHI AI sidebar (§54-§59). Every button that exists here is wired to a real
/// function (§107) — unbuilt features have no button yet, and deferred items are
/// declared in README/notes (§108 — no fake PASS).
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
    private string _lang = "ar";
    private string _currentProfileId = "";
    private readonly bool _isPrivateWindow;                     // §36 — window-wide InPrivate
    private readonly BrowserProfile? _startProfile;             // window may be opened for a profile
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

    public MainWindow() : this(null, false) { }

    /// <summary>Opened by Ctrl+N (same profile) or Ctrl+Shift+N (InPrivate window, §36/§13).</summary>
    public MainWindow(BrowserProfile? profile, bool inPrivate)
    {
        _startProfile = profile;
        _isPrivateWindow = inPrivate;
        InitializeComponent();
        TabsControl.ItemsSource = _tabs;
        // tunneling, handled-too: keys reach us even when the WebView has focus
        AddHandler(PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKey), true);
        Closing += (_, _) => CleanupTabs();
    }

    private BrowserTab? Active => _activeByProfile.TryGetValue(_currentProfileId, out var t) ? t : null;
    private string Home => SettingsStore.NormalizeHomePage(_settings.Current.HomePage);
    private SearchEngines.Engine SearchEngine => SearchEngines.Resolve(_settings.Current.SearchEngineId);

    /* ═════════════════ startup (§87/§88: window first, engine after) ═════════════════ */

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _settings = new SettingsStore();
        _lang = SettingsStore.NormalizeLanguage(_settings.Current.Language);
        _profiles = new ProfileStore(AppPaths.DataDir);
        _bookmarks = new BookmarkStore(AppPaths.DataDir);
        var def = _profiles.EnsureDefault(Loc.S(_lang, "profileDefault"));
        BookmarkBarRow.Visibility = _settings.Current.ShowBookmarksBar ? Visibility.Visible : Visibility.Collapsed;
        ApplyLanguage();
        RebuildBookmarkBar();

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

        var start = _startProfile is not null && _profiles.Find(_startProfile.ProfileId) is not null
            ? _startProfile
            : def;
        await SwitchToProfileAsync(start.ProfileId);
        AddressBox.Focus();
    }

    /* ═════════════════ profiles (§14-§20) — real isolation, real Google login ═════════════════ */

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

    private async Task AddProfileAsync()
    {
        var name = PromptWindow.Show(this, Loc.S(_lang, "profileAdd"),
            Loc.S(_lang, "profileAddNote"),
            ProfileStore.NormalizeDisplayName(null, _profiles.All.Count + 1, _lang), _lang);
        if (string.IsNullOrWhiteSpace(name)) return;

        var p = _profiles.Add(name);
        Logger.Info("profile added: " + p.DisplayName);
        await SwitchToProfileAsync(p.ProfileId);
        // real Google sign-in happens on the real Google page — never inside LATCHI UI (§16/§57)
        await NewTabAsync("https://accounts.google.com/");
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

        // close its tabs + drop its runtime state (history file we own is left on disk?
        // no — remove it too; WebView2's own profile store stays until app-data cleanup)
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

        // land somewhere real: most recently used profile that still has tabs, else default
        var next = _tabsByProfile.Where(kv => kv.Value.Count > 0)
            .OrderByDescending(kv => _profiles.Find(kv.Key)?.LastUsedUtc ?? DateTime.MinValue)
            .Select(kv => kv.Key)
            .FirstOrDefault() ?? _profiles.All[0].ProfileId;
        await SwitchToProfileAsync(next);
    }

    /* ═════════════════ tabs (§11/§12) ═════════════════ */

    private async Task NewTabAsync(string? url = null)
    {
        if (_env is null) return; // engine not ready yet — button is honest but inert

        var tab = new BrowserTab(CurrentProfile, _isPrivateWindow, _lang);
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

        _tabs.Add(tab);
        var start = url ?? Home;
        await tab.InitializeAsync(_env, start);
        if (tab.WebView is not null) ContentHost.Children.Add(tab.WebView);
        ActivateTab(tab);
    }

    private void ActivateTab(BrowserTab tab)
    {
        _activeByProfile[tab.ProfileId] = tab;
        tab.Touch();
        foreach (var t in _tabs) t.IsActive = t == tab;
        // show ONLY the active tab's webview — every other tab (including other
        // profiles' live tabs) stays alive but hidden (sessions persist, §19)
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
        else if (Active.WebView is not null) Active.WebView.Visibility = Visibility.Visible;
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
        if (!AddressBox.IsKeyboardFocused && !_addressHintOn)
            AddressBox.Text = Active.Address;
        var secure = Active.Address.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        LockGlyph.Text = secure ? "\uE72E" : "\uE7BA";
        LockGlyph.Foreground = secure
            ? (System.Windows.Media.Brush)FindResource("BrushMuted")
            : (System.Windows.Media.Brush)FindResource("BrushDanger");
    }

    private void SyncStar()
    {
        var bookmarked = Active is not null && _bookmarks is not null && _bookmarks.Contains(Active.Address);
        BtnStar.Content = bookmarked ? "\uE735" : "\uE734"; // ★ : ☆ (Segoe MDL2, §84)
        BtnStar.ToolTip = Loc.S(_lang, bookmarked ? "starRemove" : "starAdd");
    }

    /* ═════════════════ navigation (§22) ═════════════════ */

    private void OnAddressKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _addressHintOn = false;
        AddressBox.Foreground = (System.Windows.Media.Brush)FindResource("BrushText");
        var resolved = UrlHelper.ResolveAddress(AddressBox.Text, SearchEngine, Home);
        if (Active is not null)
        {
            Active.Navigate(resolved);
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
        AddressBox.Foreground = (System.Windows.Media.Brush)FindResource("BrushMuted");
    }

    private void ClearAddressHint()
    {
        if (!_addressHintOn) return;
        _addressHintOn = false;
        AddressBox.Text = "";
        AddressBox.Foreground = (System.Windows.Media.Brush)FindResource("BrushText");
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
            BookmarkBarHost.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = Loc.S(_lang, "bookmarksBarEmpty"),
                Style = (Style)FindResource("BodyText"),
                FontSize = 11.5,
                Foreground = (System.Windows.Media.Brush)FindResource("BrushMuted"),
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
            chip.Click += (_, _) => Active?.Navigate(url);
            chip.ContextMenu = new ContextMenu { FontSize = 12.5 };
            var miOpen = new MenuItem { Header = Loc.S(_lang, "bookmarkOpen") };
            miOpen.Click += (_, _) => Active?.Navigate(url);
            var miRemove = new MenuItem { Header = Loc.S(_lang, "bookmarkRemove") };
            miRemove.Click += (_, _) => { _bookmarks.Remove(url); RebuildBookmarkBar(); SyncStar(); };
            chip.ContextMenu.Items.Add(miOpen);
            chip.ContextMenu.Items.Add(miRemove);
            // right-click opens the context menu (WPF Button doesn't do it by itself)
            chip.MouseRightButtonUp += (_, e) => { chip.ContextMenu.PlacementTarget = chip; chip.ContextMenu.IsOpen = true; e.Handled = true; };
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
            border.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x4D, 0xE3, 0xB3, 0x41)); // translucent gold
        }
        else if (role == "assistant")
        {
            border.Background = (System.Windows.Media.Brush)FindResource("BrushField");
            border.BorderBrush = (System.Windows.Media.Brush)FindResource("BrushBorder");
            border.BorderThickness = new Thickness(1);
        }
        else // system / error
        {
            border.Background = (System.Windows.Media.Brush)FindResource("BrushHover");
        }

        var tb = new System.Windows.Controls.TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12.5,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            Foreground = (System.Windows.Media.Brush)FindResource("BrushText"),
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

    /* ═════════════════ child windows: downloads/history/bookmarks/extensions/settings ═════════════════ */

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
        var w = new SettingsWindow(this, _settings, OnSettingsSaved, _lang) { Owner = this };
        w.ShowDialog();
    }

    private void OnSettingsSaved()
    {
        var newLang = SettingsStore.NormalizeLanguage(_settings.Current.Language);
        var langChanged = newLang != _lang;
        _lang = newLang;
        ApplyLanguage();
        if (langChanged)
        {
            RebuildBookmarkBar();
            AiModel.Text = _settings.Current.GeminiModel;
        }
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
        else Active.Reload();
    }

    private void OnHomeClick(object sender, RoutedEventArgs e) => Active?.Navigate(Home);
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

    /* ═════════════════ keyboard (§11 + rounds 2-7 shortcuts) ═════════════════ */

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
        if (ctrl && e.Key == Key.R) { Active?.Reload(); e.Handled = true; return; }
        if (e.Key == Key.F5) { Active?.Reload(); e.Handled = true; return; }
        if (alt && e.Key == Key.Left) { Active?.GoBack(); e.Handled = true; return; }
        if (alt && e.Key == Key.Right) { Active?.GoForward(); e.Handled = true; return; }
        // rounds 2-7
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
