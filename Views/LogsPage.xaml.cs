using System.Collections.ObjectModel;
using System.Windows;
using LaraStack.User.Models;
using LaraStack.User.Services;

using UserControl = System.Windows.Controls.UserControl;

namespace LaraStack.User.Views;

public partial class LogsPage : UserControl
{
    private readonly ObservableCollection<LogEntry> _filtered = new();
    private bool _ready; // guards against calls before InitializeComponent completes

    public LogsPage()
    {
        InitializeComponent();
        _ready = true;
        LogList.ItemsSource = _filtered;
        LogService.Instance.EntryAdded += _ => Dispatcher.InvokeAsync(ApplyFilter);
        Loaded += (_, _) => ApplyFilter();
        IsVisibleChanged += (_, e) => { if ((bool)e.NewValue) ApplyFilter(); };
    }

    private void ApplyFilter()
    {
        if (!_ready) return;
        _filtered.Clear();
        var search = SearchBox.Text.Trim().ToLower();
        var level  = LevelFilter.SelectedIndex;
        foreach (var e in LogService.Instance.Entries)
        {
            if (!string.IsNullOrEmpty(search) &&
                !e.Message.ToLower().Contains(search)) continue;
            if (level > 0 && (int)e.Level + 1 != level) continue;
            _filtered.Add(e);
        }
        CountTxt.Text = $"{_filtered.Count} entries";
        if (AutoScroll.IsChecked == true && _filtered.Count > 0)
            LogList.ScrollIntoView(_filtered[^1]);
    }

    private void Filter_Changed(object s, RoutedEventArgs e) => ApplyFilter();
    private void Clear_Click(object s, RoutedEventArgs e)
    { LogService.Instance.Clear(); ApplyFilter(); }
}
