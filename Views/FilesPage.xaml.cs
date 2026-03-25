using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

using UserControl  = System.Windows.Controls.UserControl;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace LaraStack.User.Views;

public record FileItem(string Name, string Type, string Size, string Modified, string FullPath, bool IsDir);

public partial class FilesPage : UserControl
{
    private string _current = "";

    public FilesPage()
    {
        InitializeComponent();
        _current = App.Server.WebRootPath;
        Directory.CreateDirectory(_current);
        Loaded += (_, _) => Load(_current);
    }

    private void Load(string path)
    {
        _current     = path;
        PathBar.Text = path;
        UpBtn.IsEnabled = path != App.Server.WebRootPath &&
                          path.Length > App.Server.WebRootPath.Length;

        var items = new List<FileItem>();
        try
        {
            foreach (var d in Directory.GetDirectories(path))
            {
                var di = new DirectoryInfo(d);
                items.Add(new FileItem("📁  " + di.Name, "Folder", "—",
                    di.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), d, true));
            }
            foreach (var f in Directory.GetFiles(path))
            {
                var fi = new FileInfo(f);
                items.Add(new FileItem("📄  " + fi.Name,
                    fi.Extension.TrimStart('.').ToUpper(),
                    FormatSize(fi.Length),
                    fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"), f, false));
            }
        }
        catch { }
        FileList.ItemsSource = items;
    }

    private void FileList_DoubleClick(object s, MouseButtonEventArgs e)
    {
        if (FileList.SelectedItem is FileItem item)
        {
            if (item.IsDir) Load(item.FullPath);
            else try { Process.Start(new ProcessStartInfo(item.FullPath) { UseShellExecute = true }); } catch { }
        }
    }

    private void Up_Click(object s, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_current)?.FullName;
        if (parent != null) Load(parent);
    }

    private void Refresh_Click(object s, RoutedEventArgs e) => Load(_current);

    private void OpenExplorer_Click(object s, RoutedEventArgs e)
    { try { Process.Start("explorer.exe", _current); } catch { } }

    private void NewFile_Click(object s, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            InitialDirectory = _current,
            Filter = "PHP File|*.php|HTML File|*.html|All Files|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllText(dlg.FileName, "<?php\n// New file\n");
            Load(_current);
        }
    }

    private static string FormatSize(long bytes) =>
        bytes < 1024       ? $"{bytes} B" :
        bytes < 1_048_576  ? $"{bytes / 1024.0:F1} KB" :
                             $"{bytes / 1_048_576.0:F1} MB";
}
