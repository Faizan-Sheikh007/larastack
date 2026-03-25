using System.Diagnostics;
using System.Windows;
using LaraStack.User.Services;

namespace LaraStack.User.Views;

public partial class AnnouncementPopup : Window
{
    private readonly ReceivedAnnouncement _ann;

    // Auto-dismiss timer (0 = no auto-dismiss)
    private System.Windows.Threading.DispatcherTimer? _autoClose;

    public AnnouncementPopup(ReceivedAnnouncement ann)
    {
        InitializeComponent();
        _ann = ann;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Icon + accent color by type
        var (icon, color) = _ann.Type switch
        {
            1 => ("⬆", "#4A9CFF"),   // UpdateReady
            2 => ("⚠", "#FF8C42"),   // Warning
            3 => ("🚨", "#FF3B5C"),  // ForceUpdate
            _ => ("ℹ", "#9B99C0"),   // Info
        };

        IconLabel.Text    = icon;
        TypeLabel.Text    = _ann.TypeName switch
        {
            "UpdateReady" => "Update Available",
            "ForceUpdate" => "Force Update Required",
            "Warning"     => "Warning",
            _             => "Announcement"
        };
        MessageLabel.Text = _ann.Message;
        FromLabel.Text    = $"From: {_ann.AdminMachine} ({_ann.AdminIp})  ·  {FormatTime(_ann.SentAt)}";

        if (!string.IsNullOrEmpty(_ann.Version))
        {
            VersionPanel.Visibility = Visibility.Visible;
            VersionLabel.Text       = _ann.Version;
        }

        if (_ann.IsUpdate && !string.IsNullOrEmpty(_ann.DownloadUrl))
            DownloadBtn.Visibility = Visibility.Visible;

        // Position bottom-right of screen (above taskbar)
        var screen = SystemParameters.WorkArea;
        Left = screen.Right - Width - 16;
        Top  = screen.Bottom - ActualHeight - 16;

        // Force update — no dismiss button, no auto-close
        if (_ann.IsForceUpdate)
        {
            // User must acknowledge
            return;
        }

        // Auto dismiss after 12 seconds for info/warning
        _autoClose = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(12)
        };
        _autoClose.Tick += (_, _) => Close();
        _autoClose.Start();
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_ann.DownloadUrl) { UseShellExecute = true });
        }
        catch { }
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _autoClose?.Stop();
        Close();
    }

    private static string FormatTime(string iso)
    {
        return DateTime.TryParse(iso, out var dt)
            ? dt.ToString("HH:mm:ss")
            : iso;
    }
}
