using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using LatchiBrowser.Core.Services;
using Microsoft.Web.WebView2.Core;

namespace LatchiBrowser.App.Services;

/// <summary>
/// Real download manager (§27): every WebView2 download is captured (custom UI, not
/// the default one), tracked live, and supports Pause/Resume/Cancel/Open/Show-in-folder
/// — all of which are real CoreWebView2DownloadOperation operations.
/// </summary>
public sealed class DownloadItem : INotifyPropertyChanged
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string FileName { get; }
    public string FullPath { get; }
    public string Url { get; }
    internal CoreWebView2DownloadOperation Op { get; }

    private string _state = "running";
    private double _progressPct;
    private long _received;
    private long _total;

    public string State { get => _state; private set => Set(ref _state, value); }
    public double ProgressPct { get => _progressPct; private set => Set(ref _progressPct, value); }
    public long Received { get => _received; private set => Set(ref _received, value); }
    public long Total { get => _total; private set => Set(ref _total, value); }

    public bool CanPause => State == "running";
    public bool CanResume => State == "paused";
    public bool CanCancel => State is "running" or "paused";
    public bool CanOpen => State == "done";

    public event PropertyChangedEventHandler? PropertyChanged;

    internal DownloadItem(CoreWebView2DownloadOperation op)
    {
        Op = op;
        FullPath = op.ResultFilePath;
        FileName = Path.GetFileName(FullPath);
        Url = Logger.HostOnly(op.Uri ?? "");
        Sync(op);
    }

    internal void Sync(CoreWebView2DownloadOperation op)
    {
        Received = op.BytesReceived;
        Total = (long)(op.TotalBytesToReceive ?? 0);
        ProgressPct = Total > 0 ? 100.0 * Received / Total : 0;
        State = op.State switch
        {
            CoreWebView2DownloadState.InProgress => "running",
            CoreWebView2DownloadState.Completed => "done",
            CoreWebView2DownloadState.Interrupted => "interrupted",
            _ => "unknown",
        };
        // pause is a state we set ourselves (button), keep it visible until resumed
        if (_userPaused && State == "running") State = "paused";
    }

    private bool _userPaused;
    internal void MarkUserPaused(bool paused) { _userPaused = paused; OnPropertyChanged(nameof(CanPause)); }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name!));
        // state affects the button visibility too
        if (name == nameof(State)) { OnPropertyChanged(nameof(CanPause)); OnPropertyChanged(nameof(CanResume)); OnPropertyChanged(nameof(CanCancel)); OnPropertyChanged(nameof(CanOpen)); }
    }
}

public static class DownloadManager
{
    public static ObservableCollection<DownloadItem> Items { get; } = new();
    private static readonly List<Action<object?, CoreWebView2DownloadStartingEventArgs>> Wirings = new(); // keep delegates alive

    /// <summary>Wires a webview (any tab, any profile) into the shared download manager.</summary>
    public static void Wire(CoreWebView2 core)
    {
        void Handler(object? s, CoreWebView2DownloadStartingEventArgs e)
        {
            e.Handled = true; // custom tracking UI — no default download bar
            var op = e.DownloadOperation;
            var item = new DownloadItem(op);
            op.BytesReceivedChanged += (_, _) => item.Sync(op);
            op.StateChanged += (_, _) => item.Sync(op);
            Items.Insert(0, item);
            Logger.Info("download start: " + item.FileName);
        }
        core.DownloadStarting += Handler;
        Wirings.Add(Handler);
    }

    public static void Pause(DownloadItem item)
    {
        try { item.Op.Pause(); item.MarkUserPaused(true); item.Sync(item.Op); }
        catch (Exception ex) { Logger.Warn("pause failed: " + ex.Message); }
    }

    public static void Resume(DownloadItem item)
    {
        try { item.Op.Resume(); item.MarkUserPaused(false); item.Sync(item.Op); }
        catch (Exception ex) { Logger.Warn("resume failed: " + ex.Message); }
    }

    public static void Cancel(DownloadItem item)
    {
        try { item.Op.Cancel(); }
        catch (Exception ex) { Logger.Warn("cancel failed: " + ex.Message); }
    }

    public static void OpenFile(DownloadItem item)
    {
        if (item.State != "done") return;
        try { Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Warn("open failed: " + ex.Message); }
    }

    public static void ShowInFolder(DownloadItem item)
    {
        try { Process.Start("explorer.exe", $"/select,\"{item.FullPath}\""); }
        catch (Exception ex) { Logger.Warn("show-in-folder failed: " + ex.Message); }
    }

    public static void ClearFinished()
    {
        for (int i = Items.Count - 1; i >= 0; i--)
            if (Items[i].State is "done" or "cancelled" or "interrupted")
                Items.RemoveAt(i);
    }
}
