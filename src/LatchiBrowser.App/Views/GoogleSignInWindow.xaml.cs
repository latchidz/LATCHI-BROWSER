using System.Windows;
using System.Windows.Threading;
using LatchiBrowser.App.Browser;
using LatchiBrowser.App.Theme;
using LatchiBrowser.Core.Models;
using LatchiBrowser.Core.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace LatchiBrowser.App.Views;

/// <summary>
/// First-run Google gate (user request 2026-10-06): the user MUST sign in with their
/// real Google account before the browser opens. The sign-in happens on the REAL
/// accounts.google.com page inside a real WebView2 (the default profile's isolated
/// session) — LATCHI never sees, asks for or stores any password (§16/§57).
/// Success is detected when Google lands the user on myaccount.google.com.
/// </summary>
public partial class GoogleSignInWindow : Window
{
    private readonly string _lang;
    private readonly BrowserProfile _profile;
    private WebView2? _webView;
    private bool _signedIn;

    public GoogleSignInWindow(string lang, BrowserProfile profile)
    {
        InitializeComponent();
        _lang = lang;
        _profile = profile;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        FlowDirection = _lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = Loc.S(_lang, "appName");
        WelcomeTitle.Text = Loc.S(_lang, "welcomeTitle");
        WelcomeNote.Text = Loc.S(_lang, "welcomeNote");
        GoogleLabel.Text = Loc.S(_lang, "continueWithGoogle");
        SecurityNote.Text = Loc.S(_lang, "googleSecurityNote");
        DoneText.Text = Loc.S(_lang, "signedInOk");
        BtnContinue.Content = Loc.S(_lang, "confirmYes");
        FooterNote.Text = Loc.S(_lang, "googleFooterNote");
    }

    private async void OnGoogleClick(object sender, RoutedEventArgs e)
    {
        BtnGoogle.IsEnabled = false;

        // §5: same runtime check as the main window — with a clear, actionable message
        if (!BrowserEngine.IsRuntimeInstalled())
        {
            MessageBox.Show(this, Loc.S(_lang, "errRuntimeDetail"),
                Loc.S(_lang, "errRuntimeTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                    UseShellExecute = true,
                });
            }
            catch { /* best effort */ }
            BtnGoogle.IsEnabled = true;
            return;
        }

        try
        {
            var env = await BrowserEngine.GetEnvironmentAsync(_lang);

            var wv = new WebView2
            {
                CreationProperties = new CoreWebView2CreationProperties
                {
                    ProfileName = _profile.WebViewProfileName, // SAME profile the browser will use
                },
            };
            await wv.EnsureCoreWebView2Async(env);
            _webView = wv;

            var core = wv.CoreWebView2!;
            core.SourceChanged += (s, e2) =>
                Dispatcher.Invoke(() => CheckSignedIn(core.Source?.ToString() ?? ""));
            core.NavigationCompleted += (s, e2) =>
                Dispatcher.Invoke(() => CheckSignedIn(core.Source?.ToString() ?? ""));

            SignInHost.Children.Add(wv);
            WelcomePanel.Visibility = Visibility.Collapsed;
            SignInHost.Visibility = Visibility.Visible;
            core.Navigate("https://accounts.google.com/");
        }
        catch (Exception ex)
        {
            Logger.Error("sign-in webview failed: " + ex.Message);
            MessageBox.Show(this, ex.Message, Loc.S(_lang, "errRuntimeTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            BtnGoogle.IsEnabled = true;
        }
    }

    /// <summary>Google lands signed-in users on myaccount.google.com — that is the
    /// real, official signal; nothing is read from the page itself.</summary>
    private void CheckSignedIn(string url)
    {
        if (_signedIn) return;
        if (url.StartsWith("https://myaccount.google.com", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://accounts.google.com/signin/success", StringComparison.OrdinalIgnoreCase))
        {
            _signedIn = true;
            SignInHost.Visibility = Visibility.Collapsed;
            DonePanel.Visibility = Visibility.Visible;
            // give the user a beat to see the confirmation, then continue automatically
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
            t.Tick += (_, _) => { t.Stop(); Finish(); };
            t.Start();
        }
    }

    private void Finish()
    {
        if (DialogResult is null) DialogResult = true;
        Close();
    }

    private void OnContinueClick(object sender, RoutedEventArgs e) => Finish();

    protected override void OnClosed(EventArgs e)
    {
        // the webview belongs to this window only; dispose it cleanly
        try { _webView?.Dispose(); } catch { /* shutdown best-effort */ }
        base.OnClosed(e);
    }
}
