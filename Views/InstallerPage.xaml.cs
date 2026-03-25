using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using LaraStack.User.Models;
using LaraStack.User.Services;

using Color       = System.Windows.Media.Color;
using UserControl = System.Windows.Controls.UserControl;

// Only use FolderBrowserDialog from WinForms explicitly
using FolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using DialogResult        = System.Windows.Forms.DialogResult;

namespace LaraStack.User.Views;

public partial class InstallerPage : UserControl
{
    private readonly InstallerService _installer = new();
    private readonly ObservableCollection<LogEntry> _log = new();
    private CancellationTokenSource? _cts;

    public InstallerPage()
    {
        InitializeComponent();
        PathBox.Text = App.Server.BasePath;
        InstallLog.ItemsSource = _log;

        _installer.Progress += (msg, lvl) => Dispatcher.InvokeAsync(() =>
        {
            ProgressLbl.Text    = msg;
            ProgressDetail.Text = msg;
            // Avoid log spam — only add non-download-progress lines to the log list
            if (!msg.TrimStart().StartsWith("Downloading ") || lvl != LogLevel.Info)
            {
                _log.Add(new LogEntry { Timestamp = DateTime.Now, Message = msg, Level = lvl });
                if (_log.Count > 0) InstallLog.ScrollIntoView(_log[^1]);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
        _installer.ProgressPct += pct => Dispatcher.InvokeAsync(() =>
        {
            ProgressBarCtrl.Value = pct;
            ProgressPctTxt.Text   = $" {pct:F0}%";
        }, System.Windows.Threading.DispatcherPriority.Background);
        Loaded += (_, _) => RefreshStatus();
    }

    private void RefreshStatus()
    {
        var s = App.Server;
        SetStatus(PhpStatus,   PhpStatusTxt,   File.Exists(s.PhpBinPath));
        SetStatus(MySqlStatus, MySqlStatusTxt, File.Exists(s.MySqlBinPath));
        SetStatus(PmaStatus,   PmaStatusTxt,
            File.Exists(Path.Combine(s.BasePath, "www", "phpmyadmin", "index.php")));
    }

    private static void SetStatus(System.Windows.Controls.Border b,
                                   System.Windows.Controls.TextBlock t, bool ok)
    {
        t.Text = ok ? "✓  Installed" : "Not Installed";
        t.Foreground = new System.Windows.Media.SolidColorBrush(
            ok ? Color.FromRgb(0,203,113) : Color.FromRgb(107,114,128));
        b.Background = new System.Windows.Media.SolidColorBrush(
            ok ? Color.FromArgb(20,0,203,113) : Color.FromRgb(238,241,248));
    }

    private async void InstallAll_Click(object s, RoutedEventArgs e)
    {
        if (!Validate()) return;
        SetBusy(true, "Installing all…");
        _cts = new CancellationTokenSource();
        try
        {
            var p = PathBox.Text.Trim();
            await _installer.InstallPhpAsync(p, _cts.Token);
            await _installer.InstallMySqlAsync(p, _cts.Token);
            await _installer.InstallPmaAsync(p, _cts.Token);
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetBusy(false); RefreshStatus(); }
    }

    private async void InstallPhp_Click(object s, RoutedEventArgs e)
    {
        if (!Validate()) return;
        SetBusy(true, "Downloading PHP…"); _cts = new();
        try { await _installer.InstallPhpAsync(PathBox.Text.Trim(), _cts.Token); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetBusy(false); RefreshStatus(); }
    }

    private async void InstallMySql_Click(object s, RoutedEventArgs e)
    {
        if (!Validate()) return;
        SetBusy(true, "Downloading MySQL…"); _cts = new();
        try { await _installer.InstallMySqlAsync(PathBox.Text.Trim(), _cts.Token); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetBusy(false); RefreshStatus(); }
    }

    private async void InstallPma_Click(object s, RoutedEventArgs e)
    {
        if (!Validate()) return;
        SetBusy(true, "Downloading phpMyAdmin…"); _cts = new();
        try { await _installer.InstallPmaAsync(PathBox.Text.Trim(), _cts.Token); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetBusy(false); RefreshStatus(); }
    }

    private void Browse_Click(object s, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Choose LaraStack install folder" };
        if (!string.IsNullOrEmpty(PathBox.Text)) dlg.SelectedPath = PathBox.Text;
        if (dlg.ShowDialog() == DialogResult.OK) PathBox.Text = dlg.SelectedPath;
    }

    private bool Validate()
    {
        var p = PathBox.Text.Trim();
        if (string.IsNullOrEmpty(p)) { ShowError("Please enter a directory path."); return false; }
        try { Directory.CreateDirectory(p); }
        catch (Exception ex) { ShowError(ex.Message); return false; }
        return true;
    }

    private void SetBusy(bool busy, string? msg = null)
    {
        ProgressBox.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        // LogBox stays always visible — no toggling
        BtnAll.IsEnabled = BtnPhp.IsEnabled = BtnMySql.IsEnabled = BtnPma.IsEnabled = !busy;
        if (msg != null) ProgressLbl.Text = msg;
        if (!busy) ProgressBarCtrl.Value = 0;
    }

    private void ShowError(string msg)
    {
        ErrorBox.Visibility = Visibility.Visible;
        ErrorTxt.Text = msg;
    }
}
