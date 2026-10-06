using System.Windows;
using System.Windows.Controls;
using LatchiBrowser.App.Browser;
using LatchiBrowser.App.Services;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Services;

namespace LatchiBrowser.App.Views;

/// <summary>
/// Settings (§51-§53): every field here is live — language, home page, search engine,
/// AI toggle/model and the DPAPI-encrypted Gemini key. Sections we haven't built
/// (theme switching, permissions UI…) simply don't appear — no fake switches (§107).
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly Action _onSaved;
    private string _lang;
    private bool _loading = true;

    public SettingsWindow(Window owner, SettingsStore settings, Action onSaved, string lang)
    {
        InitializeComponent();
        Owner = owner;
        _settings = settings;
        _onSaved = onSaved;
        _lang = lang;

        HomeBox.Text = settings.Current.HomePage;
        ModelBox.Text = settings.Current.GeminiModel;
        ChkAi.IsChecked = settings.Current.GeminiEnabled;
        RbAr.IsChecked = lang == "ar";
        RbEn.IsChecked = lang != "ar";
        RbGoogle.IsChecked = settings.Current.SearchEngineId == "google";
        RbBing.IsChecked = settings.Current.SearchEngineId == "bing";
        RbDdg.IsChecked = settings.Current.SearchEngineId == "duckduckgo";

        ApplyLanguage();
        UpdateKeyStatus();
        _loading = false;
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
