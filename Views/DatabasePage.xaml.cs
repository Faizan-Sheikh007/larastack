using System.Windows;
using System.Windows.Threading;
using LaraStack.User.Services;

using UserControl = System.Windows.Controls.UserControl;

namespace LaraStack.User.Views;

public partial class DatabasePage : UserControl
{
    private readonly DispatcherTimer _timer;

    public DatabasePage()
    {
        InitializeComponent();
        PmaUrl.Text = $"http://localhost:{App.Server.PmaPort}";

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Loaded   += (_, _) => Refresh();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Refresh() =>
        MySqlWarning.Visibility = App.Server.IsMySqlRunning
            ? Visibility.Collapsed : Visibility.Visible;

    private void OpenPma_Click(object s, RoutedEventArgs e) =>
        ServerService.OpenUrl($"http://localhost:{App.Server.PmaPort}");

    private async void StartMySql_Click(object s, RoutedEventArgs e) =>
        await App.Server.StartMySqlAsync();
}
