using System.Windows;
using LatchiBrowser.App.Theme;

namespace LatchiBrowser.App.Views;

/// <summary>
/// First-run welcome (exactly once): choose a language, then Start Browsing — the
/// browser opens immediately. "Add Google Account" is OPTIONAL and simply opens the
/// browser and starts the add-account flow there. No gate, no forced sign-in.
/// </summary>
public partial class LanguageWindow : Window
{
    /// <summary>Returns ("ar"/"en", wantsGoogleAccount: bool), or null if closed.</summary>
    public static (string Lang, bool AddGoogle)? Ask()
    {
        var w = new LanguageWindow();
        w.ShowDialog();
        return w._result;
    }

    private (string, bool)? _result;
    private string _lang = "ar";

    public LanguageWindow()
    {
        InitializeComponent();
        // bilingual by design — both languages shown side by side
        QuestionText.Text = "اختر لغتك    ·    Choose your language";
        BtnStart.Content = "ابدأ التصفح    ·    Start Browsing";
        BtnGoogle.Content = "إضافة حساب Google (اختياري)    ·    Add Google Account (optional)";
    }

    private void Choose(string lang)
    {
        _lang = lang;
        // language only affects this button state; the result is taken on Start
        BtnArabic.Style = (Style)FindResource(lang == "ar" ? "PrimaryBtn" : "SecondaryBtn");
        BtnEnglish.Style = (Style)FindResource(lang == "en" ? "PrimaryBtn" : "SecondaryBtn");
    }

    private void OnArabicClick(object sender, RoutedEventArgs e) => Choose("ar");
    private void OnEnglishClick(object sender, RoutedEventArgs e) => Choose("en");

    private void OnStartClick(object sender, RoutedEventArgs e)
    {
        _result = (_lang, false);
        Close();
    }

    private void OnGoogleClick(object sender, RoutedEventArgs e)
    {
        _result = (_lang, true);
        Close();
    }
}
