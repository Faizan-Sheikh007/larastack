using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using LaraStack.User.Models;

namespace LaraStack.User.Services;

public class ServerService
{
    private Process? _php, _pma, _mysql;

    // PHP/PMA: tracked by process handle (they don't fork)
    public bool IsPhpRunning   => _php   != null && !_php.HasExited;
    public bool IsPmaRunning   => _pma   != null && !_pma.HasExited;

    // MySQL: mysqld.exe spawns a child and the parent exits immediately,
    // so we can't track by process handle. Use port liveness instead.
    public bool IsMySqlRunning => _mysqlStarted && IsPortListening(MySqlPort);

    // True once we've successfully kicked off mysqld (cleared on StopMySql)
    private bool _mysqlStarted = false;

    public int    PhpPort      => Config.PhpPort;
    public int    PmaPort      => Config.PhpPort + 1;   // phpMyAdmin always runs on PhpPort+1
    public int    MySqlPort    => Config.MySqlPort;
    public string BasePath     => Config.InstallPath;
    public string PhpBinPath   => Path.Combine(BasePath, "php",   "php.exe");
    public string MySqlBinPath => Path.Combine(BasePath, "mysql", "bin", "mysqld.exe");
    public string WebRootPath  => Path.Combine(BasePath, "www");
    public string PmaRootPath  => Path.Combine(BasePath, "www", "phpmyadmin");

    public UserConfig Config { get; } = UserConfig.Load();

    public event Action<string, LogLevel>? Log;
    public event Action? StatusChanged;

    public async Task StartPhpAsync()
    {
        if (IsPhpRunning) { Emit("PHP already running.", LogLevel.Warning); return; }
        if (!File.Exists(PhpBinPath)) { Emit("PHP not found. Use the Installer tab to download it.", LogLevel.Error); return; }

        if (IsPortInUse(PhpPort))
        {
            var free = FindFreePort(PhpPort);
            if (free == 0) { Emit($"Port {PhpPort} is in use and no free port was found.", LogLevel.Error); return; }
            Emit($"Port {PhpPort} is in use — switching to port {free}. Change it in Settings to make permanent.", LogLevel.Warning);
            Config.PhpPort = free;
        }

        Directory.CreateDirectory(WebRootPath);
        var idx = Path.Combine(WebRootPath, "index.php");
        if (!File.Exists(idx)) await File.WriteAllTextAsync(idx,
            "<?php echo '<h1>LaraStack is running!</h1>'; phpinfo(); ?>");

        _php = Spawn(PhpBinPath, $"-S 0.0.0.0:{PhpPort} -t \"{WebRootPath}\"", WebRootPath);
        _php.Exited += (_, _) =>
        {
            if (_php != null) // only emit if we didn't explicitly stop
                Emit("PHP stopped unexpectedly.", LogLevel.Warning);
            StatusChanged?.Invoke();
        };
        await Task.Delay(500);

        await StartPmaAsync();

        AddToUserPath(Path.GetDirectoryName(PhpBinPath)!);
        Emit($"✓ PHP running → http://localhost:{PhpPort}", LogLevel.Success);
        StatusChanged?.Invoke();
    }

    public async Task StartPmaAsync()
    {
        if (!File.Exists(PhpBinPath)) return;
        if (!Directory.Exists(PmaRootPath)) return;
        if (IsPmaRunning) return;

        int pmaPort = PmaPort;
        if (IsPortInUse(pmaPort)) pmaPort = FindFreePort(pmaPort);
        if (pmaPort == 0) { Emit("No free port for phpMyAdmin.", LogLevel.Warning); return; }

        var tmpDir = EnsureDir(BasePath, "tmp");
        await File.WriteAllTextAsync(Path.Combine(PmaRootPath, "config.inc.php"), $"""
            <?php
            $cfg['blowfish_secret'] = 'LaraStack_32charsecretkey_!!ok';
            $cfg['TempDir']         = '{tmpDir.Replace('\\', '/')}';
            $i = 0; $i++;
            $cfg['Servers'][$i]['auth_type']       = 'config';
            $cfg['Servers'][$i]['host']            = '127.0.0.1';
            $cfg['Servers'][$i]['port']            = {MySqlPort};
            $cfg['Servers'][$i]['user']            = 'root';
            $cfg['Servers'][$i]['password']        = '';
            $cfg['Servers'][$i]['AllowNoPassword'] = true;
            $cfg['Servers'][$i]['connect_type']    = 'tcp';
            """);

        _pma = Spawn(PhpBinPath, $"-S 0.0.0.0:{pmaPort} -t \"{PmaRootPath}\"", PmaRootPath);
        _pma.Exited += (_, _) =>
        {
            if (_pma != null)
                Emit("phpMyAdmin stopped.", LogLevel.Warning);
            StatusChanged?.Invoke();
        };
        await Task.Delay(400);
        Emit($"✓ phpMyAdmin → http://localhost:{pmaPort}", LogLevel.Success);
    }

    private static string EnsureDir(params string[] parts)
    {
        var path = Path.Combine(parts);
        Directory.CreateDirectory(path);
        return path;
    }

    public async Task StartMySqlAsync()
    {
        if (IsMySqlRunning) { Emit("MySQL already running.", LogLevel.Warning); return; }
        if (!File.Exists(MySqlBinPath)) { Emit("MySQL not found. Use the Installer tab to download it.", LogLevel.Error); return; }

        if (IsPortInUse(MySqlPort))
        {
            var free = FindFreePort(MySqlPort);
            if (free == 0) { Emit($"Port {MySqlPort} is in use and no free port was found.", LogLevel.Error); return; }
            Emit($"Port {MySqlPort} is in use — switching to port {free}. Change it in Settings to make permanent.", LogLevel.Warning);
            Config.MySqlPort = free;
        }

        var dataDir = Path.Combine(BasePath, "mysql", "data");
        var iniPath = Path.Combine(BasePath, "mysql", "my.ini");
        Directory.CreateDirectory(dataDir);

        await File.WriteAllTextAsync(iniPath, $"""
            [mysqld]
            basedir={Path.Combine(BasePath,"mysql").Replace('\\','/')}
            datadir={dataDir.Replace('\\','/')}
            port={MySqlPort}
            bind-address=127.0.0.1
            max_connections=100
            character-set-server=utf8mb4
            """);

        if (!Directory.EnumerateFileSystemEntries(dataDir).Any())
        {
            Emit("Initialising MySQL data directory (first run)…", LogLevel.Info);
            await RunAsync(MySqlBinPath,
                $"--initialize-insecure --basedir=\"{Path.Combine(BasePath,"mysql")}\" --datadir=\"{dataDir}\"");
        }

        _mysql = Spawn(MySqlBinPath, $"--defaults-file=\"{iniPath}\"");
        // mysqld forks — the parent process may exit immediately; we don't treat that as "stopped"
        _mysql.Exited += (_, _) => StatusChanged?.Invoke();
        _mysqlStarted = true;

        // Wait up to 8 seconds for MySQL port to come alive
        Emit("Starting MySQL…", LogLevel.Info);
        for (int i = 0; i < 16; i++)
        {
            await Task.Delay(500);
            if (IsPortListening(MySqlPort)) break;
        }

        if (!IsPortListening(MySqlPort))
        {
            Emit("MySQL did not start in time — check install or data directory.", LogLevel.Error);
            _mysqlStarted = false;
            StatusChanged?.Invoke();
            return;
        }

        // Refresh phpMyAdmin config with the real MySQL port
        if (Directory.Exists(PmaRootPath) && IsPmaRunning)
            await StartPmaAsync();

        Emit($"✓ MySQL running on port {MySqlPort}", LogLevel.Success);
        StatusChanged?.Invoke();
    }

    public async Task StartAllAsync() { await StartPhpAsync(); await StartMySqlAsync(); }

    public void StopPhp()
    {
        // Null out references BEFORE killing so the Exited handler doesn't double-log
        var pma = _pma; _pma = null;
        var php = _php; _php = null;

        if (pma != null && !pma.HasExited) { try { pma.Kill(true); pma.WaitForExit(2000); } catch { } }
        if (php != null && !php.HasExited) { try { php.Kill(true); php.WaitForExit(3000); } catch { } }

        Emit("PHP stopped.", LogLevel.Warning);
        StatusChanged?.Invoke();
    }

    public void StopMySql()
    {
        _mysqlStarted = false; // mark as intentionally stopped before killing
        var mysql = _mysql; _mysql = null;

        // 1. Try graceful shutdown via mysqladmin first
        try
        {
            var mysqladmin = Path.Combine(BasePath, "mysql", "bin", "mysqladmin.exe");
            if (File.Exists(mysqladmin))
            {
                using var admin = Process.Start(new ProcessStartInfo
                {
                    FileName  = mysqladmin,
                    Arguments = $"-u root --port={MySqlPort} --protocol=TCP shutdown",
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                })!;
                admin.WaitForExit(3000);
            }
        }
        catch { }

        // 2. Kill the tracked process handle if still alive
        if (mysql != null && !mysql.HasExited)
        {
            try { mysql.Kill(true); mysql.WaitForExit(3000); } catch { }
        }

        // 3. Kill any remaining mysqld.exe processes from our install path
        try
        {
            foreach (var p in Process.GetProcessesByName("mysqld"))
            {
                try
                {
                    string? exe = null;
                    try { exe = p.MainModule?.FileName; } catch { }
                    if (exe == null || exe.StartsWith(BasePath, StringComparison.OrdinalIgnoreCase))
                    {
                        p.Kill(true);
                        p.WaitForExit(2000);
                    }
                }
                catch { }
            }
        }
        catch { }

        Emit("MySQL stopped.", LogLevel.Warning);
        StatusChanged?.Invoke();
    }

    public void StopAll() { StopPhp(); StopMySql(); }

    public bool IsPortInUse(int port)
    {
        try { var l = new TcpListener(IPAddress.Any, port); l.Start(); l.Stop(); return false; }
        catch { return true; }
    }

    public int FindFreePort(int startPort)
    {
        for (int p = startPort + 1; p < startPort + 100; p++)
            if (!IsPortInUse(p)) return p;
        return 0;
    }

    public static bool IsPortListening(int port)
    {
        try
        {
            using var tcp = new TcpClient();
            var r  = tcp.BeginConnect("127.0.0.1", port, null, null);
            bool ok = r.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(250));
            if (ok) tcp.EndConnect(r);
            return ok;
        }
        catch { return false; }
    }

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    private Process Spawn(string exe, string args, string? workDir = null)
    {
        var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = exe, Arguments = args,
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = workDir ?? ""
            },
            EnableRaisingEvents = true
        };
        p.OutputDataReceived += (_, e) => { if (e.Data != null && !IsNoise(e.Data)) Emit(e.Data, LogLevel.Info); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data != null && !IsNoise(e.Data)) Emit(e.Data, LogLevel.Info); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    // Filter noisy lines from PHP's built-in server and MySQL startup chatter
    private static bool IsNoise(string line) =>
        line.Contains("Closed without sending a request") ||
        line.Contains("Accepted") ||
        line.Contains("Closing") ||
        line.Contains("InnoDB: Progress in percent:") ||
        line.Contains("InnoDB: Log file") ||
        (line.Contains("127.0.0.1:") && (line.Contains("Accepted") || line.Contains("Closing") || line.Contains("Closed")));

    private static async Task RunAsync(string exe, string args)
    {
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = exe, Arguments = args,
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        })!;
        await p.WaitForExitAsync();
    }

    private void Emit(string msg, LogLevel lvl)
    {
        Log?.Invoke(msg, lvl);
        LogService.Instance.Add(msg, lvl);
    }

    public void AddPhpToUserPath(string dir) => AddToUserPath(dir);

    private void AddToUserPath(string dir)
    {
        try
        {
            using var reg = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Environment", writable: true);
            if (reg == null) return;
            var current = reg.GetValue("PATH", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
            var parts = current.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(p => string.Equals(p.Trim(), dir, StringComparison.OrdinalIgnoreCase))) return;
            reg.SetValue("PATH", string.Join(';', parts.Append(dir)), Microsoft.Win32.RegistryValueKind.ExpandString);
            var procPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!procPath.Contains(dir, StringComparison.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable("PATH", procPath + ";" + dir);
            Emit($"✓ PHP added to PATH — open a NEW terminal, then run: php artisan serve", LogLevel.Success);
        }
        catch { }
    }
}

public class UserConfig
{
    public int    PhpPort     { get; set; } = 8080;
    public int    MySqlPort   { get; set; } = 3306;
    public string InstallPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LaraStack");

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LaraStack", "settings.json");

    public static UserConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var obj = JsonSerializer.Deserialize<UserConfig>(File.ReadAllText(ConfigPath));
                if (obj != null) return obj;
            }
        }
        catch { }
        return new UserConfig();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
