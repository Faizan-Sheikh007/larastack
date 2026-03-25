using System.Collections.ObjectModel;
using System.IO;
using LaraStack.User.Models;

using Application = System.Windows.Application;

namespace LaraStack.User.Services;

public class LogService
{
    public static readonly LogService Instance = new();
    private LogService() { }
    public ObservableCollection<LogEntry> Entries { get; } = new();
    public event Action<LogEntry>? EntryAdded;

    /// Path to the on-disk log file — same folder as settings.json
    public static string LogFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LaraStack", "larastack.log");

    public void Add(string msg, LogLevel lvl = LogLevel.Info)
    {
        var e   = new LogEntry { Timestamp = DateTime.Now, Message = msg, Level = lvl };
        var app = Application.Current;
        if (app?.Dispatcher.CheckAccess() == true) Push(e);
        else app?.Dispatcher.InvokeAsync(() => Push(e));
        WriteToFile(e);
    }

    private void Push(LogEntry e)
    {
        Entries.Add(e);
        if (Entries.Count > 1000) Entries.RemoveAt(0);
        EntryAdded?.Invoke(e);
    }

    private static void WriteToFile(LogEntry e)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath)!);
            // Keep log file under ~500 KB — rotate if bigger
            var fi = new FileInfo(LogFilePath);
            if (fi.Exists && fi.Length > 500_000)
            {
                var backup = LogFilePath + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(LogFilePath, backup);
            }
            File.AppendAllText(LogFilePath,
                $"[{e.Timestamp:HH:mm:ss}] [{e.Level,-7}] {e.Message}{Environment.NewLine}");
        }
        catch { /* never crash the UI over a log write */ }
    }

    public void Clear() => Entries.Clear();
}
