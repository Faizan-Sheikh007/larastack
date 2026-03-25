using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LaraStack.User.Services;

/// <summary>
/// Listens for announcements sent by the LaraStack Admin panel.
///
/// Supports three delivery modes:
///   1. Same machine  — Admin sends to 127.0.0.1:47891 (loopback)
///   2. LAN           — Admin sends UDP broadcast 255.255.255.255:47891
///   3. Internet/WAN  — Admin connects via TCP to port 47892 on this machine
///                      (requires port forwarding or same public IP)
/// </summary>
public class AnnouncementListener : IDisposable
{
    // Must match AnnouncementService.AnnouncementPort in Admin project
    public const int UdpPort = 47891;
    // TCP port for internet/direct IP delivery
    public const int TcpPort = 47892;

    private UdpClient?    _udp;
    private TcpListener?  _tcp;
    private bool          _running;
    private bool          _disposed;

    public event Action<ReceivedAnnouncement>? AnnouncementReceived;

    // -----------------------------------------------------------------------
    //  Start both UDP (LAN + same machine) and TCP (internet) listeners
    // -----------------------------------------------------------------------
    public void Start()
    {
        _running = true;
        StartUdp();
        StartTcp();
    }

    public void Stop()
    {
        _running = false;
        try { _udp?.Close(); }   catch { }
        try { _tcp?.Stop(); }    catch { }
        _udp = null;
        _tcp = null;
    }

    // -----------------------------------------------------------------------
    //  UDP — covers same machine (loopback) AND LAN broadcast
    // -----------------------------------------------------------------------
    private void StartUdp()
    {
        try
        {
            _udp = new UdpClient();

            // ReuseAddress lets multiple apps (or restarts) share the port
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Bind to ANY interface so we receive both:
            //   • 127.0.0.1  (same-machine loopback from Admin)
            //   • broadcast  (LAN from Admin on another machine)
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, UdpPort));
            _udp.EnableBroadcast = true;

            _ = UdpLoopAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AnnouncementListener] UDP failed to start on port {UdpPort}: {ex.Message}");
        }
    }

    private async Task UdpLoopAsync()
    {
        while (_running && _udp != null)
        {
            try
            {
                var result = await _udp.ReceiveAsync();
                var json   = Encoding.UTF8.GetString(result.Buffer);
                Dispatch(json);
            }
            catch (ObjectDisposedException) { break; }
            catch { await Task.Delay(500); }
        }
    }

    // -----------------------------------------------------------------------
    //  TCP — covers internet / direct IP (Admin sends to specific IP:47892)
    // -----------------------------------------------------------------------
    private void StartTcp()
    {
        try
        {
            _tcp = new TcpListener(IPAddress.Any, TcpPort);
            _tcp.Start();
            _ = TcpAcceptLoopAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AnnouncementListener] TCP failed to start on port {TcpPort}: {ex.Message}");
        }
    }

    private async Task TcpAcceptLoopAsync()
    {
        while (_running && _tcp != null)
        {
            try
            {
                var client = await _tcp.AcceptTcpClientAsync();
                _ = HandleTcpClientAsync(client); // handle each connection independently
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException)         { break; }
            catch { await Task.Delay(500); }
        }
    }

    private async Task HandleTcpClientAsync(TcpClient client)
    {
        try
        {
            using var _ = client;
            client.ReceiveTimeout = 5000;
            using var stream = client.GetStream();
            var buffer = new byte[8192];
            var read   = await stream.ReadAsync(buffer);
            if (read > 0)
            {
                var json = Encoding.UTF8.GetString(buffer, 0, read);
                Dispatch(json);
            }
        }
        catch { }
    }

    // -----------------------------------------------------------------------
    //  Common deserialization + dispatch
    // -----------------------------------------------------------------------
    private void Dispatch(string json)
    {
        try
        {
            var ann = JsonSerializer.Deserialize<ReceivedAnnouncement>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (ann != null && ann.From == "LaraStack.Admin")
                AnnouncementReceived?.Invoke(ann);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}

// ---------------------------------------------------------------------------
//  Announcement payload — must match Admin's anonymous payload fields
// ---------------------------------------------------------------------------
public class ReceivedAnnouncement
{
    public string From         { get; set; } = "";
    public string SentAt       { get; set; } = "";
    public int    Type         { get; set; }
    public string TypeName     { get; set; } = "";
    public string Message      { get; set; } = "";
    public string Version      { get; set; } = "";
    public string DownloadUrl  { get; set; } = "";
    public bool   ForceRestart { get; set; }
    public string AdminIp      { get; set; } = "";
    public string AdminMachine { get; set; } = "";

    public bool IsUpdate      => Type == 1 || Type == 3;
    public bool IsForceUpdate => Type == 3;
}
