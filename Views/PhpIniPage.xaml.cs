using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using LaraStack.User.Services;

using UserControl = System.Windows.Controls.UserControl;
using MessageBox  = System.Windows.MessageBox;
using TextBox     = System.Windows.Controls.TextBox;
using ComboBox    = System.Windows.Controls.ComboBox;

namespace LaraStack.User.Views;

public partial class PhpIniPage : UserControl
{
    private string  _iniPath   = string.Empty;
    private bool    _suspendSync; // prevent feedback loops between quick-fields and raw editor
    private bool    _isDirty;

    public PhpIniPage()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadIni();
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Load / Save
    // ──────────────────────────────────────────────────────────────────────────

    private void LoadIni()
    {
        _iniPath = ResolveIniPath();

        if (!File.Exists(_iniPath))
        {
            IniPathTxt.Text      = "php.ini not found — install PHP first.";
            NotFoundOverlay.Visibility = Visibility.Visible;
            BtnSave.IsEnabled    = false;
            BtnReload.IsEnabled  = false;
            return;
        }

        NotFoundOverlay.Visibility = Visibility.Collapsed;
        BtnSave.IsEnabled   = true;
        BtnReload.IsEnabled = true;
        IniPathTxt.Text     = _iniPath;

        var text = File.ReadAllText(_iniPath);

        _suspendSync = true;
        RawEditor.Text = text;
        RefreshQuickFields(text);
        _suspendSync = false;

        SetDirty(false);
    }

    private void SaveIni()
    {
        if (!File.Exists(_iniPath) && _iniPath.Length == 0) return;

        try
        {
            File.WriteAllText(_iniPath, RawEditor.Text);
            SetDirty(false);

            // Notify the user
            IniPathTxt.Text = $"✓  Saved — {_iniPath}";

            // Restart PHP if it's running so the new ini takes effect
            if (App.Server.IsPhpRunning)
            {
                App.Server.StopPhp();
                _ = App.Server.StartPhpAsync();
            }

            // Reset path label after a moment
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (_, _) => { IniPathTxt.Text = _iniPath; timer.Stop(); };
            timer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not save php.ini:\n{ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Quick-field ↔ raw editor synchronisation
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Read key-value pairs from raw text and populate the quick-setting boxes.</summary>
    private void RefreshQuickFields(string iniText)
    {
        TxtMemory.Text          = GetIniValue(iniText, "memory_limit")          ?? "128M";
        TxtUpload.Text          = GetIniValue(iniText, "upload_max_filesize")   ?? "2M";
        TxtPost.Text            = GetIniValue(iniText, "post_max_size")         ?? "8M";
        TxtMaxExec.Text         = GetIniValue(iniText, "max_execution_time")    ?? "30";
        TxtMaxInputTime.Text    = GetIniValue(iniText, "max_input_time")        ?? "60";
        TxtMaxFileUploads.Text  = GetIniValue(iniText, "max_file_uploads")      ?? "20";
        TxtErrorReporting.Text  = GetIniValue(iniText, "error_reporting")       ?? "E_ALL";
        TxtTimezone.Text        = GetIniValue(iniText, "date.timezone")         ?? "UTC";
        TxtSessionLife.Text     = GetIniValue(iniText, "session.gc_maxlifetime") ?? "1440";
        TxtOpcacheMem.Text      = GetIniValue(iniText, "opcache.memory_consumption") ?? "128";
        TxtDefaultCharset.Text  = GetIniValue(iniText, "default_charset")      ?? "UTF-8";

        var displayErr = GetIniValue(iniText, "display_errors") ?? "Off";
        CboDisplayErrors.SelectedIndex = displayErr.Equals("On", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

        var logErr = GetIniValue(iniText, "log_errors") ?? "On";
        CboLogErrors.SelectedIndex = logErr.Equals("On", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

        var cookieHttp = GetIniValue(iniText, "session.cookie_httponly") ?? "1";
        CboCookieHttpOnly.SelectedIndex = cookieHttp == "1" ? 0 : 1;

        var opcache = GetIniValue(iniText, "opcache.enable") ?? "1";
        CboOpcache.SelectedIndex = opcache == "1" ? 0 : 1;

        var shortTag = GetIniValue(iniText, "short_open_tag") ?? "Off";
        CboShortTag.SelectedIndex = shortTag.Equals("Off", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    /// <summary>When a quick-field changes, update the corresponding line in the raw editor.</summary>
    private void ApplyQuickFieldToRaw(string key, string value)
    {
        if (_suspendSync) return;
        _suspendSync = true;
        RawEditor.Text = SetIniValue(RawEditor.Text, key, value);
        _suspendSync = false;
        SetDirty(true);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Ini parsing helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static string? GetIniValue(string iniText, string key)
    {
        // Match   key = value   (ignoring leading semicolons / whitespace)
        var m = Regex.Match(iniText,
            $@"^[ \t]*{Regex.Escape(key)}[ \t]*=[ \t]*([^\r\n]+)",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string SetIniValue(string iniText, string key, string value)
    {
        // Replace existing uncommented assignment
        var pattern = $@"^([ \t]*{Regex.Escape(key)}[ \t]*=[ \t]*)([^\r\n]*)";
        if (Regex.IsMatch(iniText, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            return Regex.Replace(iniText, pattern, $"${{1}}{value}",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }

        // Append at end if key not found
        return iniText.TrimEnd() + $"\r\n{key} = {value}\r\n";
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Dirty-state tracking
    // ──────────────────────────────────────────────────────────────────────────

    private void SetDirty(bool dirty)
    {
        _isDirty = dirty;
        UnsavedBadge.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Path resolution
    // ──────────────────────────────────────────────────────────────────────────

    private static string ResolveIniPath()
    {
        var phpDir = Path.GetDirectoryName(App.Server.PhpBinPath) ?? string.Empty;

        // Prefer php.ini in the PHP install directory
        var candidate = Path.Combine(phpDir, "php.ini");
        if (File.Exists(candidate)) return candidate;

        // Fall back to php.ini-development / php.ini-production template if present
        foreach (var tmpl in new[] { "php.ini-development", "php.ini-production" })
        {
            var src = Path.Combine(phpDir, tmpl);
            if (File.Exists(src))
            {
                File.Copy(src, candidate);
                return candidate;
            }
        }

        // Return the expected path so the "not found" overlay is shown
        return candidate;
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  Event handlers
    // ──────────────────────────────────────────────────────────────────────────

    private void Reload_Click(object sender, RoutedEventArgs e) => LoadIni();

    private void Save_Click(object sender, RoutedEventArgs e) => SaveIni();

    private void GoInstall_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mw)
            mw.GoTo("Installer");
    }

    // Raw editor changed → update quick fields
    private void RawEditor_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suspendSync) return;
        _suspendSync = true;
        RefreshQuickFields(RawEditor.Text);
        _suspendSync = false;
        SetDirty(true);
    }

    // Quick TextBox changed → push to raw editor
    private void QuickSetting_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suspendSync || sender is not TextBox tb) return;
        var key = tb.Name switch
        {
            "TxtMemory"         => "memory_limit",
            "TxtUpload"         => "upload_max_filesize",
            "TxtPost"           => "post_max_size",
            "TxtMaxExec"        => "max_execution_time",
            "TxtMaxInputTime"   => "max_input_time",
            "TxtMaxFileUploads" => "max_file_uploads",
            "TxtErrorReporting" => "error_reporting",
            "TxtTimezone"       => "date.timezone",
            "TxtSessionLife"    => "session.gc_maxlifetime",
            "TxtOpcacheMem"     => "opcache.memory_consumption",
            "TxtDefaultCharset" => "default_charset",
            _                   => null
        };
        if (key != null) ApplyQuickFieldToRaw(key, tb.Text);
    }

    // Quick ComboBox changed → push to raw editor
    private void QuickCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendSync || sender is not ComboBox cb) return;
        if (cb.SelectedItem is not ComboBoxItem item) return;
        var val = item.Content.ToString() ?? "";
        switch (cb.Name)
        {
            case "CboDisplayErrors":  ApplyQuickFieldToRaw("display_errors",           val); break;
            case "CboLogErrors":      ApplyQuickFieldToRaw("log_errors",               val); break;
            case "CboCookieHttpOnly": ApplyQuickFieldToRaw("session.cookie_httponly",  val); break;
            case "CboOpcache":        ApplyQuickFieldToRaw("opcache.enable",           val); break;
            case "CboShortTag":       ApplyQuickFieldToRaw("short_open_tag",           val); break;
        }
    }

    // Live search / highlight (basic: scroll to first match)
    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        var term = TxtSearch.Text;
        if (string.IsNullOrWhiteSpace(term)) return;

        var idx = RawEditor.Text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return;

        RawEditor.Focus();
        RawEditor.Select(idx, term.Length);
        RawEditor.ScrollToLine(RawEditor.GetLineIndexFromCharacterIndex(idx));
    }
}
