using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using System.Windows.Threading;
using LaraStack.User.Services;
using LaraStack.User.Views;

// Disambiguate WPF vs WinForms
using Application = System.Windows.Application;
using Button      = System.Windows.Controls.Button;
using Color       = System.Windows.Media.Color;
using Ellipse     = System.Windows.Shapes.Ellipse;

namespace LaraStack.User;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _ticker;
    private readonly Button[] _navBtns;

    // Pages are lazily initialized inside the constructor (after App.Server/Metrics are ready)
    private DashboardPage  _pageDashboard  = null!;
    private ServicesPage   _pageServices   = null!;
    private InstallerPage  _pageInstaller  = null!;
    private MonitorPage    _pageMonitor    = null!;
    private LogsPage       _pageLogs       = null!;
    private FilesPage      _pageFiles      = null!;
    private DatabasePage   _pageDatabase   = null!;
    private PhpIniPage     _pagePhpIni     = null!;

    public MainWindow()
    {
        InitializeComponent();
        _navBtns = new[] { N0, N1, N2, N3, N4, N5, N6, N7 };

        // Create pages HERE — App.Server and App.Metrics are guaranteed to exist now
        _pageDashboard = new DashboardPage();
        _pageServices  = new ServicesPage();
        _pageInstaller = new InstallerPage();
        _pageMonitor   = new MonitorPage();
        _pageLogs      = new LogsPage();
        _pageFiles     = new FilesPage();
        _pageDatabase  = new DatabasePage();
        _pagePhpIni    = new PhpIniPage();

        _ticker = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _ticker.Tick += (_, _) => RefreshStatus();
        _ticker.Start();

        App.Server.StatusChanged += () => Dispatcher.InvokeAsync(() =>
        {
            RefreshStatus();
            ((App)Application.Current).WritePresence();
        });

        GoTo("Dashboard");
        RefreshStatus();
    }

    public void GoTo(string page)
    {
        PageTitle.Text = page;
        var on  = (Style)FindResource("NavBtnOn");
        var off = (Style)FindResource("NavBtn");
        foreach (var b in _navBtns)
            b.Style = (string)b.Tag == page ? on : off;

        UIElement view = page switch
        {
            "Services"  => _pageServices,
            "Installer" => _pageInstaller,
            "Monitor"   => _pageMonitor,
            "Logs"      => _pageLogs,
            "Files"     => _pageFiles,
            "Database"  => _pageDatabase,
            "PhpIni"    => _pagePhpIni,
            _           => _pageDashboard
        };
        Host.Children.Clear();
        Host.Children.Add(view);
    }

    private void RefreshStatus()
    {
        var s = App.Server;
        SetDot(DotPhp,   BadgePhp,   LblPhp,   s.IsPhpRunning,
               s.IsPhpRunning   ? $":{s.PhpPort}"   : "Stopped");
        SetDot(DotMySql, BadgeMySql, LblMySql, s.IsMySqlRunning,
               s.IsMySqlRunning ? $":{s.MySqlPort}" : "Stopped");
    }

    // Cached brushes — avoids allocating new objects every ticker tick
    private static readonly System.Windows.Media.SolidColorBrush
        _brushGreen      = Freeze(new System.Windows.Media.SolidColorBrush(Color.FromRgb(0,203,113))),
        _brushDotOff     = Freeze(new System.Windows.Media.SolidColorBrush(Color.FromRgb(51,50,96))),
        _brushLblOff     = Freeze(new System.Windows.Media.SolidColorBrush(Color.FromRgb(74,92,153))),
        _brushBadgeOn    = Freeze(new System.Windows.Media.SolidColorBrush(Color.FromArgb(40,0,203,113))),
        _brushBadgeOff   = Freeze(new System.Windows.Media.SolidColorBrush(Color.FromRgb(13,27,94)));

    private static System.Windows.Media.SolidColorBrush Freeze(System.Windows.Media.SolidColorBrush b)
        { b.Freeze(); return b; }

    private static void SetDot(Ellipse dot, System.Windows.Controls.Border badge,
                                System.Windows.Controls.TextBlock lbl, bool on, string text)
    {
        dot.Fill        = on ? _brushGreen    : _brushDotOff;
        lbl.Text        = text;
        lbl.Foreground  = on ? _brushGreen    : _brushLblOff;
        badge.Background= on ? _brushBadgeOn  : _brushBadgeOff;
    }

    private void Nav_Click(object s, RoutedEventArgs e)
    { if (s is Button b && b.Tag is string p) GoTo(p); }

    private async void StartAll_Click(object s, RoutedEventArgs e) => await App.Server.StartAllAsync();
    private void StopAll_Click(object s, RoutedEventArgs e) => App.Server.StopAll();

    private void TrayOpen_Click(object s, RoutedEventArgs e)
    { Show(); WindowState = WindowState.Normal; Activate(); }
    private async void TrayStartAll_Click(object s, RoutedEventArgs e) => await App.Server.StartAllAsync();
    private void TrayStopAll_Click(object s, RoutedEventArgs e) => App.Server.StopAll();
    private void TrayOpenWeb_Click(object s, RoutedEventArgs e) =>
        ServerService.OpenUrl($"http://localhost:{App.Server.PhpPort}");
    private void TrayExit_Click(object s, RoutedEventArgs e) =>
        ((App)Application.Current).ExitApp();

    private void TitleBar_Down(object s, System.Windows.Input.MouseButtonEventArgs e)
    { if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove(); }
    private void Minimize_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object s, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object s, RoutedEventArgs e) => ((App)Application.Current).ExitApp();

    private void Hyperlink_RequestNavigate(object s, RequestNavigateEventArgs e)
    { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); e.Handled = true; }

    private void OpenWebsite_Click(object s, System.Windows.Input.MouseButtonEventArgs e)
    { Process.Start(new ProcessStartInfo("https://cyberorion.pk") { UseShellExecute = true }); }
}
