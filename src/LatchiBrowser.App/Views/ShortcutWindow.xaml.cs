using System.Windows;
using LatchiBrowser.App.Theme;

namespace LatchiBrowser.App.Views;

/// <summary>Add / edit a Quick Access shortcut (name + url). Simple, modal, real.</summary>
public partial class ShortcutWindow : Window
{
    /// <summary>Returns (name, url) or null on cancel. url must be http(s).</summary>
    public static (string Name, string Url)? Show(Window owner, string title,
        string initialName, string initialUrl, string lang)
    {
        var w = new ShortcutWindow(owner, title, initialName, initialUrl, lang);
        w.ShowDialog();
        return w._result;
    }

    private (string, string)? _result;
    private readonly string _lang;

    private ShortcutWindow(Window owner, string title, string initialName, string initialUrl, string lang)
    {
        InitializeComponent();
        Owner = owner;
        _lang = lang;
        Title = Loc.S(lang, "appName");
        WinTitle.Text = title;
        LblName.Text = Loc.S(lang, "shortcutName");
        LblUrl.Text = Loc.S(lang, "shortcutUrl");
        NameBox.Text = initialName;
        UrlBox.Text = initialUrl;
        BtnSave.Content = Loc.S(lang, "setSave");
        BtnCancel.Content = Loc.S(lang, "confirmNo");
        Loaded += (_, _) => { NameBox.Focus(); NameBox.SelectAll(); };
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            // be forgiving: "youtube.com" → https://youtube.com
            if (Uri.TryCreate("https://" + url, UriKind.Absolute, out _)
                && url.Contains('.') && !url.Contains(' '))
                url = "https://" + url;
            else
            {
                UrlError.Text = Loc.S(_lang, "shortcutInvalidUrl");
                UrlError.Visibility = Visibility.Visible;
                UrlBox.Focus();
                return;
            }
        }
        _result = (NameBox.Text.Trim(), url);
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();
}
