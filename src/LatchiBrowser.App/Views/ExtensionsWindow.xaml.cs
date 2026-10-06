using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LatchiBrowser.App.Browser;
using LatchiBrowser.App.Theme;
using Microsoft.Web.WebView2.Core;

namespace LatchiBrowser.App.Views;

/// <summary>
/// Extensions manager (§28-§34) — built ourselves because WebView2 ships no UI for it.
/// Supports the official surface ONLY: list (GetBrowserExtensionsAsync), Enable/Disable,
/// Remove, and installing an UNPACKED extension folder (AddBrowserExtensionAsync).
/// No store, no CRX, no fake promises (documented in the note + README).
/// </summary>
public partial class ExtensionsWindow : Window
{
    private readonly BrowserTab _tab;   // extensions are per-profile → bound to the active tab's profile
    private readonly string _lang;

    public ExtensionsWindow(Window owner, BrowserTab tab, string lang)
    {
        InitializeComponent();
        Owner = owner;
        _tab = tab;
        _lang = lang;
        ApplyLanguage();
        _ = RefreshAsync();
    }

    private void ApplyLanguage()
    {
        FlowDirection = _lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = Loc.S(_lang, "extensionsTitle");
        HeaderTitle.Text = Loc.S(_lang, "extensionsTitle");
        BtnInstall.Content = Loc.S(_lang, "extInstall");
        NoteText.Text = Loc.S(_lang, "extNote");
    }

    private async Task RefreshAsync()
    {
        ListHost.Children.Clear();
        var profile = _tab.Core?.Profile;
        if (profile is null) return;

        IReadOnlyList<CoreWebView2BrowserExtension> exts;
        try { exts = await profile.GetBrowserExtensionsAsync(); }
        catch (Exception ex)
        {
            ListHost.Children.Add(new TextBlock
            {
                Text = Loc.S(_lang, "extInstallFail") + ": " + ex.Message,
                Style = (Style)FindResource("BodyText"),
                Margin = new Thickness(16),
            });
            return;
        }

        if (exts.Count == 0)
        {
            ListHost.Children.Add(new TextBlock
            {
                Text = Loc.S(_lang, "extEmpty"),
                Style = (Style)FindResource("BodyText"),
                Margin = new Thickness(16),
            });
            return;
        }

        foreach (var ext in exts)
        {
            var row = new Border { Style = (Style)FindResource("ListRow") };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();
            left.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(ext.Name) ? ext.Id : ext.Name,
                Style = (Style)FindResource("BodyText"),
                FontWeight = FontWeights.SemiBold,
            });
            var status = new TextBlock
            {
                Foreground = (Brush)FindResource("BrushMuted"),
                FontSize = 11.5,
                FontFamily = new FontFamily("Segoe UI"),
                Margin = new Thickness(0, 2, 0, 0),
                Text = ext.IsEnabled ? Loc.S(_lang, "extEnabledState") : Loc.S(_lang, "extDisabledState"),
            };
            left.Children.Add(status);
            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var btnToggle = new Button { Style = (Style)FindResource("LinkBtn") };
            btnToggle.Content = ext.IsEnabled ? Loc.S(_lang, "extDisable") : Loc.S(_lang, "extEnable");
            btnToggle.Click += async (_, _) =>
            {
                try
                {
                    // .NET wrapper exposes a single EnableAsync(bool) — one real toggle API
                    await ext.EnableAsync(!ext.IsEnabled);
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Loc.S(_lang, "extensionsTitle")); }
                await RefreshAsync();
            };

            var btnRemove = new Button
            {
                Style = (Style)FindResource("LinkBtn"),
                Content = Loc.S(_lang, "extRemove"),
                Margin = new Thickness(4, 0, 0, 0),
            };
            btnRemove.Click += async (_, _) =>
            {
                if (MessageBox.Show(this, Loc.S(_lang, "extRemoveConfirm"), Loc.S(_lang, "extensionsTitle"),
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                try { await ext.RemoveAsync(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, Loc.S(_lang, "extensionsTitle")); }
                await RefreshAsync();
            };

            actions.Children.Add(btnToggle);
            actions.Children.Add(btnRemove);
            Grid.SetColumn(actions, 1);
            grid.Children.Add(actions);

            row.Child = grid;
            ListHost.Children.Add(row);
        }
    }

    private async void OnInstallClick(object sender, RoutedEventArgs e)
    {
        // WinForms FolderBrowserDialog — the one WinForms control we use (csproj: UseWindowsForms)
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            ShowNewFolderButton = false,
        };
        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

        var profile = _tab.Core?.Profile;
        if (profile is null) return;
        try
        {
            await profile.AddBrowserExtensionAsync(dlg.SelectedPath);
            MessageBox.Show(this, Loc.S(_lang, "extInstallOk"), Loc.S(_lang, "extensionsTitle"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // usually: folder has no manifest.json / invalid manifest
            MessageBox.Show(this, Loc.S(_lang, "extInstallFail") + "\n\n" + ex.Message,
                Loc.S(_lang, "extInstallFail"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        await RefreshAsync();
    }
}
