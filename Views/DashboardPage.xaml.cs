using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using LaraStack.User.Services;

using Color       = System.Windows.Media.Color;
using UserControl = System.Windows.Controls.UserControl;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using MessageBox  = System.Windows.MessageBox;

namespace LaraStack.User.Views;

public partial class DashboardPage : UserControl
{
    private readonly DispatcherTimer _timer;

    public DashboardPage()
    {
        InitializeComponent();
        LogList.ItemsSource = LogService.Instance.Entries;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Loaded   += (_, _) => Refresh();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Refresh()
    {
        var s = App.Server;
        PhpPortTxt.Text   = s.PhpPort.ToString();
        MySqlPortTxt.Text = s.MySqlPort.ToString();
        PmaPortTxt.Text   = s.PmaPort.ToString();
        WebUrlTxt.Text    = $"http://localhost:{s.PhpPort}";
        PmaUrlTxt.Text    = $"http://localhost:{s.PmaPort}";
        WebRootTxt.Text   = s.WebRootPath;

        bool phpR = s.IsPhpRunning;
        bool sqlR = s.IsMySqlRunning;
        bool pmaR = s.IsPmaRunning;

        SetDot(PhpDot, PhpStatusTxt, phpR,
               $"PHP — :{s.PhpPort}", "PHP — not running");
        SetDot(SqlDot, SqlStatusTxt, sqlR,
               $"MySQL — :{s.MySqlPort}", "MySQL — not running");
        SetDot(PmaDot, PmaStatusTxt, pmaR,
               $"phpMyAdmin — :{s.PmaPort}", "phpMyAdmin — stopped");

        HeroBannerSub.Text = (phpR && sqlR)  ? "All services are running" :
                             (!phpR && !sqlR) ? "Start services using the sidebar buttons" :
                             $"PHP: {(phpR?"✓":"✗")}  MySQL: {(sqlR?"✓":"✗")}";

        int installed = 0;
        if (File.Exists(s.PhpBinPath))   installed++;
        if (File.Exists(s.MySqlBinPath)) installed++;
        InstallStatusTxt.Text = installed == 2 ? "All installed ✓" :
                                installed == 0 ? "Nothing installed" :
                                $"{installed}/2 installed";
        InstallStatusTxt.Foreground = new SolidColorBrush(
            installed == 2 ? Color.FromRgb(0,203,113) : Color.FromRgb(229,57,53));

        if (LogList.Items.Count > 0)
            LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private static void SetDot(System.Windows.Shapes.Ellipse dot,
                                System.Windows.Controls.TextBlock txt,
                                bool on, string onTxt, string offTxt)
    {
        dot.Fill       = new SolidColorBrush(on ? Color.FromRgb(0,203,113) : Color.FromRgb(229,57,53));
        txt.Text       = on ? onTxt : offTxt;
        txt.Foreground = new SolidColorBrush(on ? Color.FromRgb(0,203,113) : Color.FromRgb(138,150,192));
    }

    private async void StartAll_Click(object s, RoutedEventArgs e) => await App.Server.StartAllAsync();

    private void OpenWeb_Click(object s, RoutedEventArgs e) =>
        ServerService.OpenUrl($"http://localhost:{App.Server.PhpPort}");

    private void OpenPma_Click(object s, RoutedEventArgs e) =>
        ServerService.OpenUrl($"http://localhost:{App.Server.PmaPort}");

    private void OpenFolder_Click(object s, RoutedEventArgs e)
    {
        var p = App.Server.WebRootPath;
        Directory.CreateDirectory(p);
        Process.Start("explorer.exe", p);
    }

    private void GoInstall_Click(object s, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw) mw.GoTo("Installer");
    }

    /// Opens cmd.exe with PHP already injected into its PATH environment.
    /// Works immediately — no need to open a new terminal or restart anything.
    private void OpenTerminal_Click(object s, RoutedEventArgs e)
    {
        var phpDir = Path.GetDirectoryName(App.Server.PhpBinPath) ?? "";
        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";

        // Build a PATH that has the PHP directory at the front
        var newPath = phpDir + ";" + currentPath;

        var psi = new ProcessStartInfo
        {
            FileName  = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow  = false,
            // Pass /K to keep the window open after the echo, so the user can type commands
            Arguments = $"/K \"echo PHP is ready! Type: php artisan serve && echo.\"",
        };

        // Inject PHP into the PATH for this specific cmd.exe process
        psi.EnvironmentVariables["PATH"] = newPath;

        // Start in the web root so they're in a useful directory
        psi.WorkingDirectory = App.Server.WebRootPath;
        if (!Directory.Exists(psi.WorkingDirectory))
            psi.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        try
        {
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open terminal: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// Permanently adds PHP to the Windows user PATH so any CMD/PowerShell window can use php.exe.
    private void AddPhpToPath_Click(object s, RoutedEventArgs e)
    {
        var phpDir = Path.GetDirectoryName(App.Server.PhpBinPath) ?? "";

        if (!System.IO.File.Exists(App.Server.PhpBinPath))
        {
            MessageBox.Show("PHP is not installed yet. Please install PHP first using the Installer tab.",
                "PHP Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            App.Server.AddPhpToUserPath(phpDir);
            BtnAddToPath.Content   = "✓ Added";
            BtnAddToPath.IsEnabled = false;
            MessageBox.Show(
                $"PHP has been added to your Windows PATH:\n{phpDir}\n\nOpen a new CMD or PowerShell window and type 'php -v' to confirm.",
                "PHP Added to PATH", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not update PATH: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
