using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;

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

    public long TotalItems
    {
        get => _totalItems;
        set => SetProperty(ref _totalItems, value);
    }

    public SettingsViewModel(BackupPackageViewModel backupVM)
    {
        BackupVM = backupVM;
        LoadStats();
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
    private async Task ShowHelpAsync()
    {
        await System.Threading.Tasks.Task.CompletedTask;
        // TODO: Show help dialog
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
}
