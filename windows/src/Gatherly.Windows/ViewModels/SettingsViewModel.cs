using System.Diagnostics;
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;
using Microsoft.Win32;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 设置页面 ViewModel — 对齐 macOS SettingsView
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    public BackupPackageViewModel BackupVM { get; }

    private long _totalItems;
    [ObservableProperty] private string _dbSizeText = "计算中...";
    [ObservableProperty] private string _mediaSizeText = "计算中...";
    [ObservableProperty] private string _totalStorageText = "计算中...";
    [ObservableProperty] private string _dataDirectoryPath = "";
    [ObservableProperty] private bool _isCustomDirectory;
    [ObservableProperty] private string _appVersion = "1.0.0";
    [ObservableProperty] private string _restoreStatus = "";
    [ObservableProperty] private bool _hasRestoreStatus;
    [ObservableProperty] private string _dataDirStatus = "";
    [ObservableProperty] private bool _hasDataDirStatus;

    // Dry-run plan fields
    [ObservableProperty] private bool _hasMigrationPlan;
    [ObservableProperty] private string _planCurrentDir = "";
    [ObservableProperty] private string _planTargetDir = "";
    [ObservableProperty] private string _planDbSize = "";
    [ObservableProperty] private string _planMediaInfo = "";
    [ObservableProperty] private string _planEstimate = "";
    [ObservableProperty] private string _planConclusion = "";
    [ObservableProperty] private string _planWarning = "";

    // Browser selection
    [ObservableProperty] private int _selectedBrowserIndex;
    public List<BrowserEntry> AvailableBrowsers { get; } = new();
    private string SelectedBrowserBundleId => SelectedBrowserIndex > 0 && SelectedBrowserIndex < AvailableBrowsers.Count
        ? AvailableBrowsers[SelectedBrowserIndex].BundleId : "";

    public long TotalItems
    {
        get => _totalItems;
        set => SetProperty(ref _totalItems, value);
    }

    public SettingsViewModel(BackupPackageViewModel backupVM)
    {
        BackupVM = backupVM;
        LoadStats();
        LoadBrowserSettings();
    }

    public void LoadStats()
    {
        // Total items count
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DatabasePaths.DatabaseFile}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM items WHERE deleted_at IS NULL";
            TotalItems = (long)(cmd.ExecuteScalar() ?? 0);
        }
        catch { TotalItems = 0; }
        DataDirectoryPath = DatabasePaths.DataDirectory;
        IsCustomDirectory = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GATHERLY_DATA_DIR"));

        // App version
        try
        {
            var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            AppVersion = version?.ToString(3) ?? "1.0.0";
        }
        catch { AppVersion = "1.0.0"; }

        // DB size
        try
        {
            var dbFile = DatabasePaths.DatabaseFile;
            if (File.Exists(dbFile))
            {
                var size = new FileInfo(dbFile).Length;
                DbSizeText = FormatSize(size);
            }
            else
            {
                DbSizeText = "未找到";
            }
        }
        catch { DbSizeText = "未知"; }

        // Media size
        try
        {
            var mediaDir = Path.Combine(DatabasePaths.DataDirectory, "media");
            if (Directory.Exists(mediaDir))
            {
                var mediaSize = new DirectoryInfo(mediaDir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                MediaSizeText = FormatSize(mediaSize);
            }
            else
            {
                MediaSizeText = "0 B";
            }
        }
        catch { MediaSizeText = "未知"; }

        // Total storage
        try
        {
            var dbSize = File.Exists(DatabasePaths.DatabaseFile) ? new FileInfo(DatabasePaths.DatabaseFile).Length : 0;
            var mediaDir = Path.Combine(DatabasePaths.DataDirectory, "media");
            var mediaSize = Directory.Exists(mediaDir) ? new DirectoryInfo(mediaDir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
            TotalStorageText = FormatSize(dbSize + mediaSize);
        }
        catch { TotalStorageText = "未知"; }
    }

    [RelayCommand]
    private void OpenDataDirectory()
    {
        var path = DatabasePaths.DataDirectory;
        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        else
        {
            RestoreStatus = "数据目录不存在";
            HasRestoreStatus = true;
        }
    }

    [RelayCommand]
    private void ModifyDataDirectory()
    {
        // 此方法由 SettingsView.axaml.cs 的 ModifyDataDirectory_Click 调用
        // 实际的目录选择器逻辑在 code-behind 中处理
    }

    [RelayCommand]
    private async Task ShowHelpAsync()
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Avalonia.Controls.Window mainWindow)
        {
            var helpWindow = new Views.HelpWindow();
            await helpWindow.ShowDialog(mainWindow);
        }
    }

    [RelayCommand]
    private void OpenGitHub()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/V-Linkin/Archiver",
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        // TODO: Implement restore backup picker
        RestoreStatus = "还原功能开发中";
        HasRestoreStatus = true;
    }

    public long CountSync()
    {
        try
        {
            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={DatabasePaths.DatabaseFile}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM items WHERE deleted_at IS NULL";
            return (long)(cmd.ExecuteScalar() ?? 0);
        }
        catch { return 0; }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    private static readonly string BrowserSettingsPath = Path.Combine(
        Gatherly.Windows.Database.DatabasePaths.DataDirectory, "browser_settings.txt");

    private void LoadBrowserSettings()
    {
        try
        {
            AvailableBrowsers.Clear();
            AvailableBrowsers.Add(new BrowserEntry { Name = "系统默认", BundleId = "" });
            DetectBrowsers();

            var savedId = "";
            try
            {
                var settingsPath = Path.Combine(
                    Gatherly.Windows.Database.DatabasePaths.DataDirectory, "browser_settings.txt");
                if (File.Exists(settingsPath))
                    savedId = File.ReadAllText(settingsPath).Trim();
            }
            catch { }

            SelectedBrowserIndex = 0;
            for (int i = 0; i < AvailableBrowsers.Count; i++)
            {
                if (AvailableBrowsers[i].BundleId == savedId)
                {
                    SelectedBrowserIndex = i;
                    break;
                }
            }
        }
        catch
        {
            AvailableBrowsers.Clear();
            AvailableBrowsers.Add(new BrowserEntry { Name = "系统默认", BundleId = "" });
            SelectedBrowserIndex = 0;
        }
    }

    public void SaveBrowserSettings()
    {
        try
        {
            var dir = Gatherly.Windows.Database.DatabasePaths.DataDirectory;
            Directory.CreateDirectory(dir);
            File.WriteAllText(BrowserSettingsPath, SelectedBrowserBundleId);
        }
        catch { }
    }

    public string GetSelectedBrowserBundleId() => SelectedBrowserBundleId;

    private void DetectBrowsers()
    {
        var detected = new HashSet<string>();
        var logLines = new List<string> { $"[{DateTime.Now}] Browser detection start" };

        // 1. Fixed paths
        var browserPaths = new (string Name, string BundleId, string[] Paths)[]
        {
            ("Google Chrome", "chrome", new[] {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe") }),
            ("Mozilla Firefox", "firefox", new[] {
                @"C:\Program Files\Mozilla Firefox\firefox.exe",
                @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Mozilla Firefox\firefox.exe") }),
            ("Microsoft Edge", "msedge", new[] {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe" }),
            ("Brave", "brave", new[] {
                @"C:\Program Files\BraveSoftware\Brave-Browser\Application\brave.exe",
                @"C:\Program Files (x86)\BraveSoftware\Brave-Browser\Application\brave.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"BraveSoftware\Brave-Browser\Application\brave.exe") }),
            ("Arc", "arc", new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\Arc.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Arc\Arc.exe") }),
        };

        foreach (var (name, bundleId, paths) in browserPaths)
        {
            try
            {
                foreach (var p in paths) logLines.Add($"  [{name}] {p} → exists={File.Exists(p)}");
                if (paths.Any(p => File.Exists(p)) && detected.Add(bundleId))
                    AvailableBrowsers.Add(new BrowserEntry { Name = name, BundleId = bundleId });
            }
            catch (Exception ex) { logLines.Add($"  [{name}] error: {ex.Message}"); }
        }

        // 2. App Paths registry
        var appPathBrowsers = new (string Name, string BundleId, string ExeName)[]
        {
            ("Google Chrome", "chrome", "chrome.exe"),
            ("Mozilla Firefox", "firefox", "firefox.exe"),
            ("Microsoft Edge", "msedge", "msedge.exe"),
            ("Brave", "brave", "brave.exe"),
            ("Arc", "arc", "arc.exe"),
        };

        foreach (var (name, bundleId, exeName) in appPathBrowsers)
        {
            try
            {
                foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
                {
                    using var key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exeName}");
                    var val = key?.GetValue("")?.ToString();
                    logLines.Add($"  [Registry AppPaths {exeName}] root={root} → {val ?? "(null)"}");
                    if (!string.IsNullOrEmpty(val) && File.Exists(val) && detected.Add(bundleId))
                        AvailableBrowsers.Add(new BrowserEntry { Name = name, BundleId = bundleId });
                }
            }
            catch (Exception ex) { logLines.Add($"  [Registry {exeName}] error: {ex.Message}"); }
        }

        // 3. StartMenuInternet registry
        try
        {
            foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using var clientsKey = root.OpenSubKey(@"SOFTWARE\Clients\StartMenuInternet");
                if (clientsKey == null) continue;
                foreach (var subName in clientsKey.GetSubKeyNames())
                {
                    try
                    {
                        using var cmdKey = clientsKey.OpenSubKey($@"{subName}\shell\open\command");
                        var cmd = cmdKey?.GetValue("")?.ToString();
                        logLines.Add($"  [StartMenuInternet] {subName} → {cmd ?? "(null)"}");
                        if (string.IsNullOrEmpty(cmd)) continue;
                        var exePath = cmd.Split('"').Length > 1 ? cmd.Split('"')[1] : cmd.Split(' ')[0];
                        exePath = exePath.Trim();
                        if (!File.Exists(exePath)) continue;
                        var bn = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();
                        var match = appPathBrowsers.FirstOrDefault(b => bn.Contains(b.BundleId));
                        if (match.BundleId != null && detected.Add(match.BundleId))
                            AvailableBrowsers.Add(new BrowserEntry { Name = match.Name, BundleId = match.BundleId });
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex) { logLines.Add($"  [StartMenuInternet] error: {ex.Message}"); }

        logLines.Add($"[{DateTime.Now}] Final list: {string.Join(", ", AvailableBrowsers.Select(b => b.Name))}");

        // Write diagnostic log
        try
        {
            var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gatherly", "logs");
            Directory.CreateDirectory(logDir);
            File.WriteAllLines(Path.Combine(logDir, "browser-detection.log"), logLines);
        }
        catch { }
    }
}

public class BrowserEntry
{
    public string Name { get; set; } = "";
    public string BundleId { get; set; } = "";

    public override string ToString() => Name;
}
