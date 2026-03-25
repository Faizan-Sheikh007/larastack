using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LaraStack.User.Services;

/// <summary>
/// Broadcasts a UDP beacon every 5 seconds so the LaraStack Admin panel
/// can discover this User instance on the local network automatically.
/// Port: 47890 (must match InstanceMonitor.BeaconPort in Admin app)
/// </summary>
public class PresenceBroadcaster : IDisposable
{
    private const int BeaconPort     = 47890;
    private const int BroadcastMs    = 5000;

    private readonly System.Timers.Timer _timer = new(BroadcastMs);
    private UdpClient? _udp;
    private bool       _disposed;

    private readonly string _startedAt  = DateTime.Now.ToString("o");
    private readonly string _exePath    = Environment.ProcessPath ?? "";
    private readonly int    _pid        = Environment.ProcessId;

    public string InstallPath  { get; set; } = "";
    public int    PhpPort      { get; set; } = 8080;
    public int    MySqlPort    { get; set; } = 3306;
    public bool   PhpRunning   { get; set; }
    public bool   MySqlRunning { get; set; }

    public PresenceBroadcaster(string installPath, int phpPort, int mysqlPort)
    {
        InstallPath = installPath;
        PhpPort     = phpPort;
        MySqlPort   = mysqlPort;
        _timer.Elapsed += (_, _) => Broadcast();
    }

    public void Start()
    {
        try
        {
            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
            _udp.EnableBroadcast = true;
        }
        catch { }

        _timer.Start();
        Broadcast(); // immediate first beacon
    }

    public void Stop()
    {
        _timer.Stop();
        try { _udp?.Close(); } catch { }
        _udp = null;
    }

    /// Call this whenever PHP or MySQL status changes so Admin sees it immediately
    public void UpdateStatus(bool phpRunning, bool mysqlRunning)
    {
        PhpRunning   = phpRunning;
        MySqlRunning = mysqlRunning;
        Broadcast();
    }

    private void Broadcast()
    {
        try
        {
            if (_udp == null || _disposed) return;

            var payload = new
            {
                AppName      = "LaraStack.User",
                Pid          = _pid,
                PhpPort,
                MySqlPort,
                PhpRunning,
                MySqlRunning,
                MachineUser  = Environment.UserName,
                MachineName  = Environment.MachineName,
                LocalIp      = GetLocalIp(),
                InstallPath,
                ExePath      = _exePath,
                StartedAt    = _startedAt,
                Version      = "2.0.0",
            };

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            _udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, BeaconPort));
        }
        catch { }
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

    public void Dispose()
    {
        _disposed = true;
        Stop();
        _timer.Dispose();
    }
}
