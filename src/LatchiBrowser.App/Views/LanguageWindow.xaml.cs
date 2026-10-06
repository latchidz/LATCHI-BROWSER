using System.Windows;
using LatchiBrowser.App.Theme;

namespace LatchiBrowser.App.Views;

/// <summary>
/// First-run language gate (user request 2026-10-06): a clean branded chooser shown
/// exactly once, before anything else. Returns the chosen language via <see cref="Ask"/>.
/// </summary>
public partial class LanguageWindow : Window
{
    public static string? Ask()
    {
        var w = new LanguageWindow();
        w.ShowDialog();
        return w._result;
    }

    private string? _result;

    public LanguageWindow()
    {
        InitializeComponent();
        // bilingual by design — both languages shown side by side
        QuestionText.Text = "اختر لغتك    ·    Choose your language";
        Closing += (_, e) => { if (_result is null) e.Cancel = false; };
    }

    private void Choose(string lang)
    {
        _result = lang;
        Close();
    }

    private void OnArabicClick(object sender, RoutedEventArgs e) => Choose("ar");
    private void OnEnglishClick(object sender, RoutedEventArgs e) => Choose("en");
}
