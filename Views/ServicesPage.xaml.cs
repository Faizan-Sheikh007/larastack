using System.IO;
using System.Windows;
using System.Windows.Threading;
using LaraStack.User.Services;

using Color       = System.Windows.Media.Color;
using UserControl = System.Windows.Controls.UserControl;

namespace LaraStack.User.Views;

public partial class ServicesPage : UserControl
{
    private readonly DispatcherTimer _timer;
    public ServicesPage()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Loaded   += (_, _) => Refresh();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        var s = App.Server;
        PhpBinTxt.Text    = s.PhpBinPath;
        PhpPortTxt.Text   = s.PhpPort.ToString();
        PhpPortBox.Text   = s.PhpPort.ToString();
        PhpRootTxt.Text   = s.WebRootPath;
        PhpUrlTxt.Text    = $"http://localhost:{s.PhpPort}";
        MySqlBinTxt.Text  = s.MySqlBinPath;
        MySqlPortTxt.Text = s.MySqlPort.ToString();
        MySqlPortBox.Text = s.MySqlPort.ToString();
        MySqlDataTxt.Text = Path.Combine(s.BasePath, "mysql", "data");

        bool phpInstalled = File.Exists(s.PhpBinPath);
        bool sqlInstalled = File.Exists(s.MySqlBinPath);
        bool phpR = s.IsPhpRunning;
        bool sqlR = s.IsMySqlRunning;

        PhpMissing.Visibility   = phpInstalled ? Visibility.Collapsed : Visibility.Visible;
        MySqlMissing.Visibility = sqlInstalled ? Visibility.Collapsed : Visibility.Visible;

        SetBadge(PhpBadge,   PhpBadgeTxt,   phpR);
        SetBadge(MySqlBadge, MySqlBadgeTxt, sqlR);

        PhpStartBtn.IsEnabled   = !phpR && phpInstalled;
        PhpStopBtn.IsEnabled    = phpR;
        PhpRestBtn.IsEnabled    = phpR;
        MySqlStartBtn.IsEnabled = !sqlR && sqlInstalled;
        MySqlStopBtn.IsEnabled  = sqlR;
        MySqlRestBtn.IsEnabled  = sqlR;
    }

    private static void SetBadge(System.Windows.Controls.Border b,
                                  System.Windows.Controls.TextBlock t, bool on)
    {
        t.Text = on ? "RUNNING" : "STOPPED";
        t.Foreground = new System.Windows.Media.SolidColorBrush(
            on ? Color.FromRgb(0,203,113) : Color.FromRgb(107,114,128));
        b.Background = new System.Windows.Media.SolidColorBrush(
            on ? Color.FromArgb(25,0,203,113) : Color.FromRgb(238,241,248));
    }

    private async void PhpStart_Click(object s, RoutedEventArgs e)    => await App.Server.StartPhpAsync();
    private void        PhpStop_Click(object s, RoutedEventArgs e)     => App.Server.StopPhp();
    private async void PhpRestart_Click(object s, RoutedEventArgs e)
    { App.Server.StopPhp(); await Task.Delay(300); await App.Server.StartPhpAsync(); }

    private async void MySqlStart_Click(object s, RoutedEventArgs e)   => await App.Server.StartMySqlAsync();
    private void        MySqlStop_Click(object s, RoutedEventArgs e)    => App.Server.StopMySql();
    private async void MySqlRestart_Click(object s, RoutedEventArgs e)
    { App.Server.StopMySql(); await Task.Delay(300); await App.Server.StartMySqlAsync(); }

    private void GoInstall_Click(object s, RoutedEventArgs e)
    { if (Window.GetWindow(this) is MainWindow mw) mw.GoTo("Installer"); }

    private void PhpPortBox_LostFocus(object s, RoutedEventArgs e)
    {
        if (int.TryParse(PhpPortBox.Text, out int p) && p is > 0 and < 65536)
        {
            App.Server.Config.PhpPort = p;
            App.Server.Config.Save();
            Refresh();
        }
    }

    private void MySqlPortBox_LostFocus(object s, RoutedEventArgs e)
    {
        if (int.TryParse(MySqlPortBox.Text, out int p) && p is > 0 and < 65536)
        {
            App.Server.Config.MySqlPort = p;
            App.Server.Config.Save();
            Refresh();
        }
    }
}
