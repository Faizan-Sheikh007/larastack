using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using LaraStack.User.Services;
using LaraStack.User.Views;

using Application = System.Windows.Application;

namespace LaraStack.User;

public partial class App : Application
{
    public static ServerService           Server      { get; private set; } = null!;
    public static MetricsService          Metrics     { get; private set; } = null!;
    public static PresenceBroadcaster     Broadcaster { get; private set; } = null!;
    public static AnnouncementListener    Announcements { get; private set; } = null!;

    /// Presence file read by the Admin app to detect this running instance (local only)
    public static string PresenceFile =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LaraStack", "presence.json");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Server  = new ServerService();
        Metrics = new MetricsService();
        Metrics.Start();

        // Start UDP presence broadcaster so Admin can discover us on the LAN
        Broadcaster = new PresenceBroadcaster(
            installPath : Server.BasePath,
            phpPort     : Server.PhpPort,
            mysqlPort   : Server.MySqlPort);

        // Keep broadcaster in sync whenever server status changes
        Server.StatusChanged += OnServerStatusChanged;

        Broadcaster.Start();
        WritePresence();

        // Start announcement listener — receives messages from Admin
        // Works on same machine (loopback), LAN (UDP broadcast), and internet (TCP)
        Announcements = new AnnouncementListener();
        Announcements.AnnouncementReceived += OnAnnouncementReceived;
        Announcements.Start();

        new MainWindow().Show();
    }

    private void OnAnnouncementReceived(ReceivedAnnouncement ann)
    {
        // Always dispatch to UI thread
        Dispatcher.Invoke(() =>
        {
            var popup = new AnnouncementPopup(ann);
            popup.Show();
        });
    }

    private void OnServerStatusChanged()
    {
        // Update broadcaster ports in case they changed (e.g. port conflict fallback)
        Broadcaster.PhpPort   = Server.PhpPort;
        Broadcaster.MySqlPort = Server.MySqlPort;
        Broadcaster.UpdateStatus(Server.IsPhpRunning, Server.IsMySqlRunning);

        // Also refresh presence.json for local Admin detection
        WritePresence();
    }

    public void ExitApp()
    {
        Metrics.Stop();
        Broadcaster.Stop();
        Announcements.Stop();
        Server.StopAll();
        DeletePresence();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Metrics.Stop();
        Broadcaster.Stop();
        Announcements.Stop();
        Server.StopAll();
        DeletePresence();
        base.OnExit(e);
    }

    internal void WritePresence()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PresenceFile)!);
            var data = new
            {
                Pid         = Environment.ProcessId,
                StartedAt   = DateTime.Now.ToString("o"),
                PhpPort     = Server.PhpPort,
                MySqlPort   = Server.MySqlPort,
                InstallPath = Server.BasePath,
                LogFile     = LogService.LogFilePath,
                MachineUser = Environment.UserName,
                MachineName = Environment.MachineName,
                LocalIp     = GetLocalIp(),
                ExePath     = Environment.ProcessPath ?? "",
                // Expose TCP announcement port so Admin knows how to reach us over internet
                AnnouncementUdpPort = AnnouncementListener.UdpPort,
                AnnouncementTcpPort = AnnouncementListener.TcpPort,
            };
            File.WriteAllText(PresenceFile,
                JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static void DeletePresence()
    {
        try { if (File.Exists(PresenceFile)) File.Delete(PresenceFile); } catch { }
    }

    private static string GetLocalIp()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        return addr.Address.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }
}
