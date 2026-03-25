using System.Windows.Media;

using Color = System.Windows.Media.Color;

namespace LaraStack.User.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string   Message   { get; set; } = "";
    public LogLevel Level     { get; set; }
    public string TimestampStr => Timestamp.ToString("HH:mm:ss");
    public string LevelTag  => Level switch { LogLevel.Success=>"OK", LogLevel.Warning=>"WARN", LogLevel.Error=>"ERR", _=>"INFO" };
    public System.Windows.Media.Brush LevelBrush => Level switch
    {
        LogLevel.Success => new SolidColorBrush(Color.FromRgb(0,203,113)),
        LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255,140,66)),
        LogLevel.Error   => new SolidColorBrush(Color.FromRgb(229,57,53)),
        _                => new SolidColorBrush(Color.FromRgb(155,153,192))
    };
    public System.Windows.Media.Brush LevelBgBrush => Level switch
    {
        LogLevel.Success => new SolidColorBrush(Color.FromArgb(30,0,203,113)),
        LogLevel.Warning => new SolidColorBrush(Color.FromArgb(30,255,140,66)),
        LogLevel.Error   => new SolidColorBrush(Color.FromArgb(30,229,57,53)),
        _                => new SolidColorBrush(Color.FromArgb(30,155,153,192))
    };
}
public enum LogLevel { Info, Success, Warning, Error }
