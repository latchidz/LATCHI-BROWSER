using System.ComponentModel;
using System.Runtime.CompilerServices;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LatchiBrowser.App.Browser;

/// <summary>
/// One real browser tab = one WebView2 created from the SHARED environment (§12).
/// The tab model carries everything the spec requires: TabId, ProfileId, Url, Title,
/// Favicon, IsPinned, IsActive, LastUsed. The WebView itself is created lazily —
/// never before the tab exists (§4). Sessions live as long as the tab lives.
/// </summary>
public sealed class BrowserTab : IDisposable, INotifyPropertyChanged
{
    private string _title;
    private string _address = "";
    private string? _faviconUrl;
    private bool _isLoading;
    private bool _canGoBack;
    private bool _canGoForward;
    private bool _isActive;
    private bool _isPinned;
    private DateTime _lastUsedUtc = DateTime.UtcNow;

    public BrowserTab(LatchiBrowser.Core.Models.BrowserProfile profile, bool isInPrivate, string language)
    {
        Profile = profile;
        IsInPrivate = isInPrivate;
        _title = Loc.S(language, isInPrivate ? "privateTab" : "newTab");
    }

    // ── identity & model (§12) ──────────────────────────────────────────
    public string TabId { get; } = Guid.NewGuid().ToString("N");
    /// <summary>Profile this tab belongs to (§14) — full profile model, not just an id.</summary>
    public LatchiBrowser.Core.Models.BrowserProfile Profile { get; }
    public string ProfileId => Profile.ProfileId;
    /// <summary>Private tabs (§36) run InPrivate under the same profile — and never record history.</summary>
    public bool IsInPrivate { get; }
    public bool IsPinned { get => _isPinned; set => Set(ref _isPinned, value); }
    public DateTime LastUsedUtc { get => _lastUsedUtc; private set => Set(ref _lastUsedUtc, value); }

    // ── live state ──────────────────────────────────────────────────────
    public WebView2? WebView { get; private set; }
    public CoreWebView2? Core => WebView?.CoreWebView2;

    public string Title { get => _title; private set => Set(ref _title, value); }
    public string Address { get => _address; private set => Set(ref _address, value); }
    public string? FaviconUrl { get => _faviconUrl; private set => Set(ref _faviconUrl, value); }
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }
    public bool CanGoBack { get => _canGoBack; private set => Set(ref _canGoBack, value); }
    public bool CanGoForward { get => _canGoForward; private set => Set(ref _canGoForward, value); }
    public bool IsActive { get => _isActive; set => Set(ref _isActive, value); }

    // ── windowing hooks ─────────────────────────────────────────────────
    /// <summary>A page asked for a new window (target=_blank / window.open) — real browsers open a tab; so do we (§47).</summary>
    public event Action<BrowserTab, string>? PopupRequested;
    /// <summary>A page entered/left HTML5 fullscreen — e.g. video players (§8: video fullscreen ≠ browser fullscreen).</summary>
    public event Action<BrowserTab, bool>? ContentFullscreenChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name!));
    }

    /// <summary>A real navigation completed — the window records it into the profile's history (§26).
    /// Private tabs never raise this for history purposes (§36).</summary>
    public event Action<BrowserTab, string, string>? NavigationCommitted;

    /// <summary>Creates the WebView from the shared environment (never a new environment, §4/§82).
    /// The tab's profile (name + InPrivate) is applied through CreationProperties BEFORE
    /// initialization — that is how WebView2 puts this webview under the right isolated
    /// profile while staying inside the one shared browser process set (§14/§19).</summary>
    public async Task InitializeAsync(CoreWebView2Environment environment, string startUrl)
    {
        if (WebView is not null) return;
        var wv = new WebView2
        {
            CreationProperties = new CoreWebView2CreationProperties
            {
                ProfileName = Profile.WebViewProfileName,
                IsInPrivateModeEnabled = IsInPrivate,
            },
        };
        await wv.EnsureCoreWebView2Async(environment);
        WebView = wv;
        var core = wv.CoreWebView2!;

        // real downloads, tracked by our own manager (§27)
        Services.DownloadManager.Wire(core);

        core.DocumentTitleChanged += (s, e) =>
        {
            if (!string.IsNullOrEmpty(core.DocumentTitle)) Title = core.DocumentTitle;
        };
        core.SourceChanged += (s, e) => Address = core.Source?.ToString() ?? "";
        core.NavigationStarting += (s, e) => IsLoading = true;
        core.NavigationCompleted += (s, e) =>
        {
            IsLoading = false;
            SyncHistory();
            // record into per-profile history — private tabs excluded (§36)
            if (!IsInPrivate && !string.IsNullOrEmpty(Address))
                NavigationCommitted?.Invoke(this, Title, Address);
        };
        core.HistoryChanged += (s, e) => SyncHistory();
        core.FaviconChanged += (s, e) => FaviconUrl = core.FaviconUri;
        core.NewWindowRequested += (s, e) =>
        {
            // handled synchronously on purpose — WebView2 requires the decision before returning
            e.Handled = true;
            if (!string.IsNullOrEmpty(e.Uri)) PopupRequested?.Invoke(this, e.Uri!);
        };
        core.ContainsFullScreenElementChanged += (s, e) =>
            ContentFullscreenChanged?.Invoke(this, core.ContainsFullScreenElement);

        Logger.Info($"tab {TabId[..6]} opened → {Logger.HostOnly(startUrl)}");
        core.Navigate(startUrl);
    }

    // ── commands (all no-op safely before init) ─────────────────────────
    public void Navigate(string url)
    {
        if (Core is { } core) core.Navigate(url);
    }

    public void GoBack() => Core?.GoBack();
    public void GoForward() => Core?.GoForward();
    public void Reload() => Core?.Reload();
    public void Stop() => Core?.Stop();

    /// <summary>Mark as the currently-used tab (§12 LastUsed — feeds future suspension logic).</summary>
    public void Touch() => LastUsedUtc = DateTime.UtcNow;

    private void SyncHistory()
    {
        if (Core is not { } core) return;
        CanGoBack = core.CanGoBack;
        CanGoForward = core.CanGoForward;
    }

    public void Dispose()
    {
        if (WebView is null) return;
        try
        {
            // the WPF control hosts the webview in an HwndHost — disposing it destroys
            // the native window, which closes that webview (per WebView2 WPF design)
            WebView.Dispose();
        }
        catch
        {
            // disposal must never throw during shutdown
        }
        finally
        {
            WebView = null;
        }
        Logger.Info($"tab {TabId[..6]} closed");
    }
}
