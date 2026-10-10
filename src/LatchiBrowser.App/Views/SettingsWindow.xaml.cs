using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LatchiBrowser.App.Browser;
using LatchiBrowser.App.Services;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Services;

namespace LatchiBrowser.App.Views;

/// <summary>
/// Settings (§51-§53): every field here is live — language, home page, search engine,
/// appearance (bookmarks bar + home background), tabs restore, privacy (real
/// WebView2 data clearing), AI toggle/model and the DPAPI-encrypted Gemini key.
/// Not-built features (light theme, ask-before-download) simply don't appear (§107).
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly Action _onSaved;
    private readonly Action<string> _onCustomize;   // background: "dark"|"image"|"remove"|#hex
    private readonly Action _onClearCache;
    private readonly Action _onClearCookies;
    private readonly Action _onClearHistory;
    private string _lang;
    private bool _loading = true;

    private static readonly (string Name, string Hex)[] Palette =
    {
        ("#FF0B1E3D", "bgNavy"),
        ("#FF2A1B3D", "bgViolet"),
        ("#FF0F2B22", "bgGreen"),
        ("#FF20242B", "bgGray"),
        ("#FF331422", "bgWine"),
    };

    public SettingsWindow(Window owner, SettingsStore settings, Action onSaved, string lang,
        Action<string> onCustomize, Action onClearCache, Action onClearCookies, Action onClearHistory)
    {
        InitializeComponent();
        Owner = owner;
        _settings = settings;
        _onSaved = onSaved;
        _onCustomize = onCustomize;
        _onClearCache = onClearCache;
        _onClearCookies = onClearCookies;
        _onClearHistory = onClearHistory;
        _lang = lang;

        HomeBox.Text = settings.Current.HomePage;
        ModelBox.Text = settings.Current.GeminiModel;
        ChkAi.IsChecked = settings.Current.GeminiEnabled;
        ChkShowBar.IsChecked = settings.Current.ShowBookmarksBar;
        ChkRestoreTabs.IsChecked = settings.Current.RestoreTabsOnStartup;
        RbAr.IsChecked = lang == "ar";
        RbEn.IsChecked = lang != "ar";
        RbGoogle.IsChecked = settings.Current.SearchEngineId == "google";
        RbBing.IsChecked = settings.Current.SearchEngineId == "bing";
        RbDdg.IsChecked = settings.Current.SearchEngineId == "duckduckgo";

        BuildColorSwatches();

        ApplyLanguage();
        UpdateKeyStatus();
        _loading = false;
    }

    private void BuildColorSwatches()
    {
        foreach (var (hex, key) in Palette)
        {
            var hexCopy = hex;
            var swatch = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = Loc.S(_lang, key),
            };
            swatch.MouseLeftButtonUp += (_, _) => _onCustomize(hexCopy); // applied instantly, like real browsers
            ColorRow.Children.Add(swatch);
        }
    }

    private void ApplyLanguage()
    {
        FlowDirection = _lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = Loc.S(_lang, "settingsTitle");
        HeaderTitle.Text = Loc.S(_lang, "settingsTitle");
        LblGeneral.Text = Loc.S(_lang, "setGeneral");
        LblLanguage.Text = Loc.S(_lang, "setLanguage");
        RbAr.Content = "العربية";
        RbEn.Content = "English";
        LblHome.Text = Loc.S(_lang, "setHome");
        LblSearch.Text = Loc.S(_lang, "setSearch");
        RbGoogle.Content = "Google";
        RbBing.Content = "Bing";
        RbDdg.Content = "DuckDuckGo";
        LblAppearance.Text = Loc.S(_lang, "setAppearance");
        ChkShowBar.Content = Loc.S(_lang, "setShowBar");
        LblBg.Text = Loc.S(_lang, "setBg");
        BtnBgDark.Content = Loc.S(_lang, "bgDark");
        BtnBgImage.Content = Loc.S(_lang, "bgImage");
        BtnBgRemove.Content = Loc.S(_lang, "bgRemove");
        LblTabs.Text = Loc.S(_lang, "setTabs");
        ChkRestoreTabs.Content = Loc.S(_lang, "restoreTabs");
        LblPrivacy.Text = Loc.S(_lang, "setPrivacy");
        BtnClearHistory.Content = Loc.S(_lang, "privacyHistory");
        BtnClearCache.Content = Loc.S(_lang, "privacyCache");
        BtnClearCookies.Content = Loc.S(_lang, "privacyCookies");
        LblAi.Text = Loc.S(_lang, "setAi");
        ChkAi.Content = Loc.S(_lang, "setAiEnable");
        LblModel.Text = Loc.S(_lang, "setModel");
        LblKey.Text = Loc.S(_lang, "setKey");
        KeyBox.Password = ""; // never prefill the stored key — it stays invisible
        KeyNote.Text = Loc.S(_lang, "setKeyNote");
        BtnRemoveKey.Content = Loc.S(_lang, "setKeyRemove");
        BtnRemoveKey.IsEnabled = SecureKeyStore.HasKey();
        LblAbout.Text = Loc.S(_lang, "setAbout");
        AboutText.Text =
            $"LATCHI Browser v{App.Version}\nWebView2 Runtime {(BrowserEngine.IsRuntimeInstalled() ? BrowserEngine.RuntimeVersion : "-")}\n" +
            Loc.S(_lang, "extNote");
        BtnSave.Content = Loc.S(_lang, "setSave");
        BtnCancel.Content = Loc.S(_lang, "setCancel");
        UpdateKeyStatus();
    }

    private void UpdateKeyStatus()
    {
        KeyStatus.Text = SecureKeyStore.HasKey() ? Loc.S(_lang, "setKeyStored") : "";
    }

    private void OnKeyChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) BtnRemoveKey.IsEnabled = SecureKeyStore.HasKey() || KeyBox.SecurePassword.Length > 0;
    }

    // language radio switches the dialog itself too — instant feedback, §77
    private void OnLangChecked(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var newLang = RbEn.IsChecked == true ? "en" : "ar";
        if (newLang == _lang) return;
        _lang = newLang;
        ApplyLanguage();
    }

    private void OnEngineChecked(object sender, RoutedEventArgs e) { /* selection read on save */ }

    private void OnAiChecked(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            ModelBox.IsEnabled = ChkAi.IsChecked == true;
            KeyBox.IsEnabled = ChkAi.IsChecked == true;
        }
    }

    private void OnRemoveKeyClick(object sender, RoutedEventArgs e)
    {
        SecureKeyStore.DeleteKey();
        KeyBox.Password = "";
        BtnRemoveKey.IsEnabled = false;
        UpdateKeyStatus();
    }

    // ── appearance: instant background, handled by the main window ──

    private void OnBgDarkClick(object sender, RoutedEventArgs e) => _onCustomize("dark");
    private void OnBgImageClick(object sender, RoutedEventArgs e) => _onCustomize("image");
    private void OnBgRemoveClick(object sender, RoutedEventArgs e) => _onCustomize("remove");

    // ── privacy: real clearing ──

    private void OnClearHistoryClick(object sender, RoutedEventArgs e) => _onClearHistory();
    private void OnClearCacheClick(object sender, RoutedEventArgs e) => _onClearCache();
    private void OnClearCookiesClick(object sender, RoutedEventArgs e) => _onClearCookies();

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _settings.Current.Language = _lang;
        _settings.Current.HomePage = SettingsStore.NormalizeHomePage(HomeBox.Text);
        _settings.Current.SearchEngineId = RbBing.IsChecked == true ? "bing"
            : RbDdg.IsChecked == true ? "duckduckgo"
            : "google";
        _settings.Current.GeminiEnabled = ChkAi.IsChecked == true;
        _settings.Current.GeminiModel = string.IsNullOrWhiteSpace(ModelBox.Text)
            ? Gemini.DefaultModel : ModelBox.Text.Trim();
        _settings.Current.ShowBookmarksBar = ChkShowBar.IsChecked == true;
        _settings.Current.RestoreTabsOnStartup = ChkRestoreTabs.IsChecked == true;
        _settings.Save();

        // key: stored only through DPAPI — never in settings.json (§56/§57)
        if (KeyBox.SecurePassword.Length > 0)
        {
            try { SecureKeyStore.SaveKey(KeyBox.Password); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, Loc.S(_lang, "settingsTitle"));
            }
        }

        _onSaved();
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
