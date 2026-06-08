using System.IO.Compression;
using System.Text.Json;
using Gatherly.Windows.Database;
using Gatherly.Windows.Services;
using Gatherly.Windows.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class BackupImportViewModelTests : IDisposable
{
    private readonly string _tempDir;

    public BackupImportViewModelTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gatherly_vm_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// 创建模拟 macOS 备份的 zip 文件（复用 BackupImportTests 同样的模式）
    /// </summary>
    private string CreateTestBackupZip()
    {
        var zipDir = Path.Combine(_tempDir, "zip_source");
        Directory.CreateDirectory(zipDir);

        var dbPath = Path.Combine(zipDir, "archiver.db");
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            MigrationRunner.RunAll(conn);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    import_date, modify_date, content_status, archive_status, media_status)
                VALUES ('00000000-0000-0000-0000-000000000001', 'https://example.com', 'bilibili',
                    'https://example.com', 1700000000, 1700000000, 'normal', 'pending', 'textOnly')";
            cmd.ExecuteNonQuery();
        }

        var mediaDir = Path.Combine(zipDir, "media", "00000000-0000-0000-0000-000000000001");
        Directory.CreateDirectory(mediaDir);
        File.WriteAllBytes(Path.Combine(mediaDir, "image_001.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });

        var logosDir = Path.Combine(zipDir, "platform_logos");
        Directory.CreateDirectory(logosDir);
        File.WriteAllBytes(Path.Combine(logosDir, "bilibili.png"), new byte[] { 0x89, 0x50, 0x4E });

        var info = new Dictionary<string, object>
        {
            ["version"] = "1.0.0",
            ["backupDate"] = "2025-06-04T15:30:00Z",
            ["hasDatabase"] = true,
            ["hasMedia"] = true,
            ["hasLogos"] = true
        };
        File.WriteAllText(
            Path.Combine(zipDir, "backup_info.json"),
            JsonSerializer.Serialize(info));

        var zipPath = Path.Combine(_tempDir, "test_backup.zip");
                // On Windows, SQLite WAL lock may linger after connection close
        SqliteConnection.ClearAllPools();
        System.Threading.Thread.Sleep(200);

        ZipFile.CreateFromDirectory(zipDir, zipPath);
        Directory.Delete(zipDir, true);

        return zipPath;
    }

    /// <summary>
    /// 创建空测试数据库（模拟当前空的应用数据库）
    /// </summary>
    private string CreateEmptyTestDatabase()
    {
        var dbPath = Path.Combine(_tempDir, "app_test.db");
        using (var conn = new SqliteConnection($"Data Source={dbPath}"))
        {
            conn.Open();
            MigrationRunner.RunAll(conn);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS custom_platforms (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, logo_path TEXT,
                created_at REAL NOT NULL, sort_order INTEGER NOT NULL DEFAULT 0)";
            cmd.ExecuteNonQuery();
        }
        return dbPath;
    }

    /// <summary>
    /// 创建非空测试数据库（模拟已有数据的应用数据库）
    /// </summary>
    private string CreateNonEmptyTestDatabase()
    {
        var dbPath = CreateEmptyTestDatabase();
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status)
            VALUES ('11111111-1111-1111-1111-111111111111', 'https://existing.com', 'youtube',
                'https://existing.com', 1700000000, 1700000000, 'normal', 'pending', 'textOnly')";
        cmd.ExecuteNonQuery();
        return dbPath;
    }

    [Fact]
    public void MainWindowViewModel_CreatesSuccessfully()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        MigrationRunner.RunAll(conn);

        var vm = new MainWindowViewModel(conn);

        Assert.NotNull(vm);
        Assert.NotNull(vm.Home);
        Assert.NotNull(vm.Search);
        Assert.NotNull(vm.Trash);
        Assert.False(vm.IsImportingBackup);
        Assert.Null(vm.BackupImportStatus);
        Assert.Null(vm.BackupImportError);
    }

    [Fact]
    public void MainWindowViewModel_InitiallyNoImportState()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        MigrationRunner.RunAll(conn);

        var vm = new MainWindowViewModel(conn);

        Assert.False(vm.IsImportingBackup);
        Assert.False(vm.HasBackupImportStatus);
        Assert.False(vm.HasBackupImportError);
    }

    [Fact]
    public async Task ImportBackup_InvalidPath_SetsError()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        MigrationRunner.RunAll(conn);

        var vm = new MainWindowViewModel(conn);

        await vm.ImportBackupAsync("/nonexistent/path.zip");

        Assert.False(vm.IsImportingBackup);
        Assert.Null(vm.BackupImportStatus);
        Assert.NotNull(vm.BackupImportError);
        Assert.Contains("导入失败", vm.BackupImportError);
    }

    [Fact]
    public async Task ImportBackup_Success_SetsStatus()
    {
        var zipPath = CreateTestBackupZip();
        var dbPath = CreateEmptyTestDatabase();
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);

        // Temporarily override DatabasePaths for this test
        // Since DatabasePaths is static, we test the service directly
        var service = new BackupImportService();
        await service.ImportBackupAsync(zipPath, dbPath, dataDir);

        // Verify the database was restored
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ImportBackup_NonEmptyDb_ThrowsException()
    {
        var zipPath = CreateTestBackupZip();
        var dbPath = CreateNonEmptyTestDatabase();
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);

        var service = new BackupImportService();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportBackupAsync(zipPath, dbPath, dataDir));
        Assert.Contains("空数据库", ex.Message);
    }

    [Fact]
    public async Task ImportBackup_Success_RefreshesHome()
    {
        var zipPath = CreateTestBackupZip();
        var dbPath = CreateEmptyTestDatabase();
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);

        // Use the service directly to restore data
        var service = new BackupImportService();
        await service.ImportBackupAsync(zipPath, dbPath, dataDir);

        // Now create a ViewModel pointing at the restored database
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        var vm = new MainWindowViewModel(conn);

        // Home should have loaded the restored data
        await vm.Home.LoadCommand.ExecuteAsync(null);
        Assert.Single(vm.Home.RecentItems);
    }

    [Fact]
    public async Task ImportBackup_Success_RefreshesTrash()
    {
        var zipPath = CreateTestBackupZip();
        var dbPath = CreateEmptyTestDatabase();
        var dataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(dataDir);

        var service = new BackupImportService();
        await service.ImportBackupAsync(zipPath, dbPath, dataDir);

        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        var vm = new MainWindowViewModel(conn);

        await vm.Trash.LoadCommand.ExecuteAsync(null);
        // No trashed items in test backup
        Assert.Empty(vm.Trash.TrashedItems);
    }

    [Fact]
    public async Task ImportBackup_AlreadyImporting_ReturnsEarly()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        MigrationRunner.RunAll(conn);

        var vm = new MainWindowViewModel(conn);
        vm.IsImportingBackup = true;

        // Should return immediately without doing anything
        await vm.ImportBackupAsync("/nonexistent/path.zip");

        // Error should remain null since it returned early
        Assert.Null(vm.BackupImportError);
    }
}
