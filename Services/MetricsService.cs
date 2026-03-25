using System.Diagnostics;
using System.IO;
using System.Management;

namespace LaraStack.User.Services;

public class MetricsService
{
    private PerformanceCounter? _cpu;
    private readonly System.Timers.Timer _t = new(2000); // 2s — WMI is slow, no need for 1s
    private readonly Queue<double> _cpuQ = new(60);
    private readonly Queue<double> _ramQ = new(60);
    private ManagementObjectSearcher? _wmi; // reuse — creating new searcher every second is expensive
    public event Action<MetricSnap>? Updated;

    public MetricsService()
    {
        try { _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total"); } catch { }
        try { _wmi = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem"); } catch { }
        _t.Elapsed += (_, _) => Collect();
    }

    public void Start() { _t.Start(); Collect(); }
    public void Stop()  => _t.Stop();

    private void Collect()
    {
        try
        {
            double cpu = 0;
            try { cpu = _cpu?.NextValue() ?? 0; } catch { }

            double total = 0, free = 0;
            try
            {
                if (_wmi != null)
                    foreach (ManagementObject o in _wmi.Get())
                    {
                        total = Convert.ToDouble(o["TotalVisibleMemorySize"]) / 1024.0;
                        free  = Convert.ToDouble(o["FreePhysicalMemory"])     / 1024.0;
                    }
            }
            catch { }

            double usedRam = total - free;
            double ramPct  = total > 0 ? usedRam / total * 100 : 0;

            var drv       = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
            double diskUsed  = drv != null ? (drv.TotalSize - drv.AvailableFreeSpace) / 1_073_741_824.0 : 0;
            double diskTotal = drv != null ? drv.TotalSize / 1_073_741_824.0 : 0;
            double diskPct   = diskTotal > 0 ? diskUsed / diskTotal * 100 : 0;

            if (_cpuQ.Count >= 60) _cpuQ.Dequeue(); _cpuQ.Enqueue(cpu);
            if (_ramQ.Count >= 60) _ramQ.Dequeue(); _ramQ.Enqueue(ramPct);

            Updated?.Invoke(new MetricSnap(cpu, usedRam, total, ramPct,
                diskUsed, diskTotal, diskPct,
                _cpuQ.ToArray(), _ramQ.ToArray()));
        }
        catch { }
    }
}

public record MetricSnap(
    double Cpu, double RamUsed, double RamTotal, double RamPct,
    double DiskUsed, double DiskTotal, double DiskPct,
    double[] CpuHist, double[] RamHist);
