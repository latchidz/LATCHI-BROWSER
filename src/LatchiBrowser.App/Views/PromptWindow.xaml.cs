using System.Windows;
using System.Windows.Input;
using LatchiBrowser.App.Theme;

namespace LatchiBrowser.App.Views;

/// <summary>Tiny modal text prompt (profile add/rename) — styled like the rest of LATCHI.</summary>
public partial class PromptWindow : Window
{
    /// <summary>Returns the entered text, or null when cancelled.</summary>
    public static string? Show(Window owner, string title, string note, string initial, string lang)
    {
        var w = new PromptWindow(owner, title, note, initial, lang);
        w.ShowDialog();
        return w._result;
    }

    private string? _result;

    private PromptWindow(Window owner, string title, string note, string initial, string lang)
    {
        InitializeComponent();
        Owner = owner;
        Title = title;
        PromptLabel.Text = title;
        if (!string.IsNullOrEmpty(note))
        {
            PromptNote.Text = note;
            PromptNote.Visibility = Visibility.Visible;
        }
        PromptBox.Text = initial;
        FlowDirection = lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        BtnOk.Content = Loc.S(lang, "confirmYes");
        BtnCancel.Content = Loc.S(lang, "confirmNo");
        Loaded += (_, _) => { PromptBox.Focus(); PromptBox.SelectAll(); };
    }

    private void OnBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Accept(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
    }

    private void Accept()
    {
        _result = PromptBox.Text.Trim();
        Close();
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Accept();
    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
