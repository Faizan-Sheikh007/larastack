using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using LaraStack.User.Services;

using Color       = System.Windows.Media.Color;
using UserControl = System.Windows.Controls.UserControl;

namespace LaraStack.User.Views;

public partial class MonitorPage : UserControl
{
    private double[]? _lastCpu, _lastRam;

    public MonitorPage()
    {
        InitializeComponent();
        App.Metrics.Updated += OnMetrics;
        Unloaded += (_, _) => App.Metrics.Updated -= OnMetrics;
    }

    private void OnMetrics(MetricSnap s)
    {
        Dispatcher.InvokeAsync(() =>
        {
            CpuVal.Text    = $"{s.Cpu:F0}%";
            CpuBar.Value   = s.Cpu;
            RamVal.Text    = $"{s.RamPct:F0}%";
            RamBar.Value   = s.RamPct;
            RamDetail.Text = $"{s.RamUsed:F0} / {s.RamTotal:F0} MB";
            DiskVal.Text   = $"{s.DiskPct:F0}%";
            DiskBar.Value  = s.DiskPct;
            DiskDetail.Text = $"{s.DiskUsed:F0} / {s.DiskTotal:F0} GB";

            var sv = App.Server;
            SvcPhp.Text = $"PHP    {(sv.IsPhpRunning   ? "● RUNNING" : "○ Stopped")}";
            SvcPhp.Foreground = new System.Windows.Media.SolidColorBrush(
                sv.IsPhpRunning ? Color.FromRgb(0,203,113) : Color.FromRgb(107,114,128));
            SvcMySql.Text = $"MySQL  {(sv.IsMySqlRunning ? "● RUNNING" : "○ Stopped")}";
            SvcMySql.Foreground = new System.Windows.Media.SolidColorBrush(
                sv.IsMySqlRunning ? Color.FromRgb(0,203,113) : Color.FromRgb(107,114,128));

            _lastCpu = s.CpuHist;
            _lastRam = s.RamHist;

            DrawChart(CpuChart,     s.CpuHist, Color.FromRgb(0,203,113),  null);
            DrawChart(RamChart,     s.RamHist, Color.FromRgb(160,32,240), null);
            DrawChart(OverlayChart, s.CpuHist, Color.FromRgb(0,203,113),
                      s.RamHist, Color.FromRgb(160,32,240));
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void Chart_SizeChanged(object s, SizeChangedEventArgs e)
    {
        if (_lastCpu == null || _lastRam == null) return;
        if (s == CpuChart)
            DrawChart(CpuChart,     _lastCpu, Color.FromRgb(0,203,113),  null);
        else if (s == RamChart)
            DrawChart(RamChart,     _lastRam, Color.FromRgb(160,32,240), null);
        else if (s == OverlayChart)
            DrawChart(OverlayChart, _lastCpu, Color.FromRgb(0,203,113),
                      _lastRam, Color.FromRgb(160,32,240));
    }

    private static void DrawChart(Canvas canvas, double[] data, Color color,
        double[]? data2 = null, Color? color2 = null)
    {
        canvas.Children.Clear();
        double w = canvas.ActualWidth, h = canvas.ActualHeight;
        if (w <= 0 || h <= 0 || data.Length < 2) return;

        // Grid lines
        foreach (var pct in new[] { 0.25, 0.5, 0.75 })
        {
            var line = new Line
            {
                X1 = 0, X2 = w, Y1 = h * (1 - pct), Y2 = h * (1 - pct),
                Stroke = new System.Windows.Media.SolidColorBrush(
                    Color.FromArgb(30, 100, 100, 150)),
                StrokeThickness = 1,
                StrokeDashArray = new System.Windows.Media.DoubleCollection { 4, 4 }
            };
            canvas.Children.Add(line);
        }

        DrawSeries(canvas, data, color, w, h, false);
        if (data2 != null && color2.HasValue)
            DrawSeries(canvas, data2, color2.Value, w, h, true);
    }

    private static void DrawSeries(Canvas canvas, double[] data, Color color,
        double w, double h, bool dashed)
    {
        double step = w / Math.Max(data.Length - 1, 1);
        var pts = data.Select((v, i) => new System.Windows.Point(i * step, h - v / 100.0 * h)).ToList();

        // Fill
        var fill = new Polygon
        {
            Fill = new System.Windows.Media.SolidColorBrush(
                Color.FromArgb(dashed ? (byte)15 : (byte)35, color.R, color.G, color.B)),
            StrokeThickness = 0
        };
        fill.Points.Add(new System.Windows.Point(0, h));
        foreach (var pt in pts) fill.Points.Add(pt);
        fill.Points.Add(new System.Windows.Point(w, h));
        canvas.Children.Add(fill);

        // Line
        var poly = new Polyline
        {
            Stroke          = new System.Windows.Media.SolidColorBrush(color),
            StrokeThickness = dashed ? 1.5 : 2,
            StrokeLineJoin  = System.Windows.Media.PenLineJoin.Round
        };
        if (dashed) poly.StrokeDashArray = new System.Windows.Media.DoubleCollection { 6, 3 };
        foreach (var pt in pts) poly.Points.Add(pt);
        canvas.Children.Add(poly);
    }
}
