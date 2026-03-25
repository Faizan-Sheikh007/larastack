using System.IO;
using System.IO.Compression;
using System.Net.Http;
using LaraStack.User.Models;

namespace LaraStack.User.Services;

public class InstallerService
{
    private static readonly string[] PhpUrls =
    {
        // Stable "latest" redirect — always points to current release, never goes stale
        "https://windows.php.net/downloads/releases/latest/php-8.3-nts-Win32-vs16-x64-latest.zip",
        // Pinned fallbacks (current as of 2025-12)
        "https://windows.php.net/downloads/releases/php-8.3.29-nts-Win32-vs16-x64.zip",
        "https://windows.php.net/downloads/releases/latest/php-8.2-nts-Win32-vs16-x64-latest.zip",
        "https://windows.php.net/downloads/releases/php-8.2.30-nts-Win32-vs16-x64.zip",
    };
    private static readonly string[] MySqlUrls =
    {
        // cdn.mysql.com serves direct ZIPs without redirect/consent pages
        "https://cdn.mysql.com/Downloads/MySQL-8.0/mysql-8.0.44-winx64.zip",
        "https://cdn.mysql.com/Downloads/MySQL-8.0/mysql-8.0.42-winx64.zip",
        "https://cdn.mysql.com/Downloads/MySQL-8.0/mysql-8.0.40-winx64.zip",
    };
    private static readonly string[] PmaUrls =
    {
        "https://files.phpmyadmin.net/phpMyAdmin/5.2.2/phpMyAdmin-5.2.2-all-languages.zip",
        "https://files.phpmyadmin.net/phpMyAdmin/5.2.1/phpMyAdmin-5.2.1-all-languages.zip",
    };

    private readonly HttpClient _http;
    public event Action<string, LogLevel>? Progress;
    public event Action<double>?           ProgressPct;

    public InstallerService()
    {
        _http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true })
            { Timeout = TimeSpan.FromMinutes(30) };
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/122 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "*/*");
    }

    public async Task InstallPhpAsync(string basePath, CancellationToken ct = default)
    {
        var dest = EnsureDir(basePath, "php");
        var zip  = TempPath("php.zip");
        Report("Downloading PHP 8.3…", LogLevel.Info);
        await DownloadWithFallbackAsync(PhpUrls, zip, ct);
        Report("Extracting PHP…", LogLevel.Info);
        SafeExtract(zip, dest); TryDelete(zip);
        var sample = Path.Combine(dest, "php.ini-development");
        var ini    = Path.Combine(dest, "php.ini");
        if (File.Exists(sample))
        {
            var txt = await File.ReadAllTextAsync(sample, ct);
            foreach (var ext in new[] { "mysqli","pdo_mysql","mbstring","curl","openssl","zip","gd","intl","fileinfo" })
                txt = txt.Replace($";extension={ext}", $"extension={ext}");
            txt = txt.Replace(";extension_dir = \"ext\"", "extension_dir = \"ext\"");
            await File.WriteAllTextAsync(ini, txt, ct);
        }
        Report("✓ PHP installed!", LogLevel.Success);
    }

    public async Task InstallMySqlAsync(string basePath, CancellationToken ct = default)
    {
        var zip = TempPath("mysql.zip"); var tmp = TempPath("mysql_tmp");
        Report("Downloading MySQL 8.0…", LogLevel.Info);
        await DownloadWithFallbackAsync(MySqlUrls, zip, ct);
        Report("Extracting MySQL…", LogLevel.Info);
        SafeExtractToTemp(zip, tmp);
        var src = Directory.GetDirectories(tmp).FirstOrDefault();
        var dst = Path.Combine(basePath, "mysql");
        if (src != null) { if (Directory.Exists(dst)) Directory.Delete(dst, true); Directory.Move(src, dst); }
        TryDelete(tmp, true); TryDelete(zip);
        Report("✓ MySQL installed!", LogLevel.Success);
    }

    public async Task InstallPmaAsync(string basePath, CancellationToken ct = default)
    {
        var zip = TempPath("pma.zip"); var tmp = TempPath("pma_tmp");
        Report("Downloading phpMyAdmin…", LogLevel.Info);
        await DownloadWithFallbackAsync(PmaUrls, zip, ct);
        Report("Extracting phpMyAdmin…", LogLevel.Info);
        SafeExtractToTemp(zip, tmp);
        var src = Directory.GetDirectories(tmp).FirstOrDefault();
        var dst = EnsureDir(basePath, "www", "phpmyadmin");
        if (src != null) { if (Directory.Exists(dst)) Directory.Delete(dst, true); Directory.Move(src, dst); }
        EnsureDir(basePath, "tmp"); // create tmp dir for PMA sessions
        // Note: config.inc.php is written by ServerService.StartPmaAsync() at runtime
        // so the MySQL port is always current
        TryDelete(tmp, true); TryDelete(zip);
        Report("✓ phpMyAdmin installed!", LogLevel.Success);
    }

    private async Task DownloadWithFallbackAsync(string[] urls, string dest, CancellationToken ct)
    {
        Exception? last = null;
        foreach (var url in urls)
        {
            try { Report($"  → {Path.GetFileName(url)}", LogLevel.Info); await DownloadAsync(url, dest, ct); return; }
            catch (Exception ex) { last = ex; Report($"  ✗ {ex.Message}", LogLevel.Warning); TryDelete(dest); }
        }
        throw new Exception($"All mirrors failed. Last: {last?.Message}");
    }

    private async Task DownloadAsync(string url, string dest, CancellationToken ct)
    {
        TryDelete(dest);
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"HTTP {(int)resp.StatusCode}");

        var total = resp.Content.Headers.ContentLength ?? -1L;
        long done = 0;

        // Throttle: only fire progress every 200 ms to avoid flooding the UI dispatcher
        var sw         = System.Diagnostics.Stopwatch.StartNew();
        long lastReport = 0;
        long lastDone   = 0;

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        {
            await using var out_ = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true);
            var buf = new byte[131072]; int read;
            while ((read = await src.ReadAsync(buf, ct)) > 0)
            {
                await out_.WriteAsync(buf.AsMemory(0, read), ct);
                done += read;

                var now = sw.ElapsedMilliseconds;
                if (now - lastReport >= 200) // update UI at most 5x per second
                {
                    var pct     = total > 0 ? done * 100.0 / total : 0;
                    var elapsed = (now - lastReport) / 1000.0;
                    var speed   = elapsed > 0 ? (done - lastDone) / elapsed : 0;
                    var speedMb = speed / (1024.0 * 1024.0);
                    lastReport  = now;
                    lastDone    = done;

                    ProgressPct?.Invoke(pct);

                    if (total > 0)
                    {
                        var doneMb  = done  / (1024.0 * 1024.0);
                        var totalMb = total / (1024.0 * 1024.0);
                        Report($"  Downloading {Path.GetFileName(url)} — {doneMb:F1} / {totalMb:F1} MB  ({speedMb:F2} MB/s)", LogLevel.Info);
                    }
                }
            }
            await out_.FlushAsync(ct);
        }
        ProgressPct?.Invoke(100);

        // Validate ZIP magic bytes
        using var fs = File.OpenRead(dest);
        var magic = new byte[4]; fs.Read(magic, 0, 4);
        if (magic[0] != 0x50 || magic[1] != 0x4B)
            throw new Exception("Not a valid ZIP archive — download may be incomplete or server returned HTML.");
    }

    private static void SafeExtract(string zip, string dst)
    { if (!Directory.Exists(dst)) Directory.CreateDirectory(dst); ZipFile.ExtractToDirectory(zip, dst, true); }

    private static void SafeExtractToTemp(string zip, string tmp)
    { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); Directory.CreateDirectory(tmp); ZipFile.ExtractToDirectory(zip, tmp, true); }

    private static string EnsureDir(params string[] parts)
    { var p = Path.Combine(parts); Directory.CreateDirectory(p); return p; }

    private static string TempPath(string n) => Path.Combine(Path.GetTempPath(), "LaraStack_" + n);

    private static void TryDelete(string path, bool dir = false)
    {
        try { if (dir) { if (Directory.Exists(path)) Directory.Delete(path, true); } else { if (File.Exists(path)) File.Delete(path); } } catch { }
    }

    private void Report(string msg, LogLevel lvl) { Progress?.Invoke(msg, lvl); LogService.Instance.Add(msg, lvl); }
}
