using System.IO.Compression;
using System.Text.Json;
using Gatherly.Windows.Services;
using Gatherly.Windows.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class BackupImportTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _targetDbPath;
    private readonly string _targetDataDir;

    public BackupImportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"gatherly_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _targetDbPath = Path.Combine(_tempDir, "target.db");
        _targetDataDir = Path.Combine(_tempDir, "data");
        Directory.CreateDirectory(_targetDataDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, true); } catch { }
        }
    }

    /// <summary>
    /// 创建一个模拟 macOS 备份的 zip 文件
    /// </summary>
    private string CreateTestBackupZip(
        bool includeDb = true,
        bool includeMedia = true,
        bool includeLogos = true,
        bool includeBackupInfo = true)
    {
        var zipDir = Path.Combine(_tempDir, "zip_source");
        Directory.CreateDirectory(zipDir);

        // 创建 archiver.db
        if (includeDb)
        {
            var dbPath = Path.Combine(zipDir, "archiver.db");
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                MigrationRunner.RunAll(conn);

                // 插入测试数据
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO items (id, original_url, platform, normalized_url,
                            import_date, modify_date, content_status, archive_status, media_status)
                        VALUES ('00000000-0000-0000-0000-000000000001', 'https://example.com', 'bilibili',
                            'https://example.com', 1700000000, 1700000000, 'normal', 'pending', 'textOnly')";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO folders (id, name, platform, created_at, sort_order)
                        VALUES ('00000000-0000-0000-0000-000000000010', 'Test Folder', 'bilibili', 1700000000, 0)";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT INTO media_assets (id, item_id, type, file_name, file_size, download_status, created_at)
                        VALUES ('00000000-0000-0000-0000-000000000020',
                            '00000000-0000-0000-0000-000000000001', 'image', 'test.jpg', 1024, 'completed', 1700000000)";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 创建 media/ 目录
        if (includeMedia)
        {
            var mediaDir = Path.Combine(zipDir, "media", "00000000-0000-0000-0000-000000000001");
            Directory.CreateDirectory(mediaDir);
            File.WriteAllBytes(Path.Combine(mediaDir, "image_001.jpg"), new byte[] { 0xFF, 0xD8, 0xFF });
        }

        // 创建 platform_logos/ 目录
        if (includeLogos)
        {
            var logosDir = Path.Combine(zipDir, "platform_logos");
            Directory.CreateDirectory(logosDir);
            File.WriteAllBytes(Path.Combine(logosDir, "bilibili.png"), new byte[] { 0x89, 0x50, 0x4E });
        }

        // 创建 backup_info.json
        if (includeBackupInfo)
        {
            var info = new Dictionary<string, object>
            {
                ["version"] = "1.0.0",
                ["backupDate"] = "2025-06-04T15:30:00Z",
                ["hasDatabase"] = includeDb,
                ["hasMedia"] = includeMedia,
                ["hasLogos"] = includeLogos
            };
            File.WriteAllText(
                Path.Combine(zipDir, "backup_info.json"),
                JsonSerializer.Serialize(info));
        }

        // 打包为 zip
        var zipPath = Path.Combine(_tempDir, "test_backup.zip");
        ZipFile.CreateFromDirectory(zipDir, zipPath);

        // 清理源目录
        Directory.Delete(zipDir, true);

        return zipPath;
    }

    // ==================== 核心恢复测试 ====================

    [Fact]
    public async Task ImportBackup_RestoresItemsTable()
    {
        var zipPath = CreateTestBackupZip();
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        using var conn = new SqliteConnection($"Data Source={_targetDbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ImportBackup_RestoresFoldersTable()
    {
        var zipPath = CreateTestBackupZip();
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        using var conn = new SqliteConnection($"Data Source={_targetDbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM folders";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ImportBackup_RestoresMediaAssetsTable()
    {
        var zipPath = CreateTestBackupZip();
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        using var conn = new SqliteConnection($"Data Source={_targetDbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM media_assets";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ImportBackup_RestoresMediaFiles()
    {
        var zipPath = CreateTestBackupZip();
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        var mediaFile = Path.Combine(_targetDataDir, "media",
            "00000000-0000-0000-0000-000000000001", "image_001.jpg");
        Assert.True(File.Exists(mediaFile));
    }

    [Fact]
    public async Task ImportBackup_RestoresPlatformLogos()
    {
        var zipPath = CreateTestBackupZip();
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        var logoFile = Path.Combine(_targetDataDir, "platform_logos", "bilibili.png");
        Assert.True(File.Exists(logoFile));
    }

    // ==================== FTS5 测试 ====================

    [Fact]
    public async Task ImportBackup_FtsRebuildWorks()
    {
        var zipPath = CreateTestBackupZip();
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        // FTS5 查询不应报错
        using var conn = new SqliteConnection($"Data Source={_targetDbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items_fts";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.True(count >= 0); // FTS 表存在即可
    }

    // ==================== 错误处理测试 ====================

    [Fact]
    public async Task ImportBackup_MissingDbFile_ThrowsException()
    {
        var zipPath = CreateTestBackupZip(includeDb: false);
        var service = new BackupImportService();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir));
    }

    [Fact]
    public async Task ImportBackup_NonexistentZip_ThrowsException()
    {
        var service = new BackupImportService();

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => service.ImportBackupAsync("/nonexistent/path.zip", _targetDbPath, _targetDataDir));
    }

    // ==================== 不覆盖已有文件测试 ====================

    [Fact]
    public async Task ImportBackup_DoesNotOverwriteExistingMedia()
    {
        // 先创建目标媒体文件
        var existingMediaDir = Path.Combine(_targetDataDir, "media",
            "00000000-0000-0000-0000-000000000001");
        Directory.CreateDirectory(existingMediaDir);
        var existingFile = Path.Combine(existingMediaDir, "image_001.jpg");
        File.WriteAllBytes(existingFile, new byte[] { 0x00, 0x00, 0x00 });

        var zipPath = CreateTestBackupZip();
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        // 文件应该还是原来的 3 字节
        var content = File.ReadAllBytes(existingFile);
        Assert.Equal(3, content.Length);
    }

    // ==================== 备份信息读取测试 ====================

    [Fact]
    public async Task ImportBackup_ReadsBackupInfo()
    {
        var zipPath = CreateTestBackupZip();
        var service = new BackupImportService();

        // 应该不报错（backup_info.json 可选）
        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);
    }

    [Fact]
    public async Task ImportBackup_MissingBackupInfo_StillWorks()
    {
        var zipPath = CreateTestBackupZip(includeBackupInfo: false);
        var service = new BackupImportService();

        // 缺少 backup_info.json 不应阻止恢复
        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        using var conn = new SqliteConnection($"Data Source={_targetDbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(1, count);
    }

    // ==================== 缺少 media/logos 测试 ====================

    [Fact]
    public async Task ImportBackup_MissingMedia_StillWorks()
    {
        var zipPath = CreateTestBackupZip(includeMedia: false);
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        using var conn = new SqliteConnection($"Data Source={_targetDbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ImportBackup_MissingLogos_StillWorks()
    {
        var zipPath = CreateTestBackupZip(includeLogos: false);
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        using var conn = new SqliteConnection($"Data Source={_targetDbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items";
        var count = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.Equal(1, count);
    }

    // ==================== 恢复后数据可读测试 ====================

    [Fact]
    public async Task ImportBackup_RestoredDataIsReadable()
    {
        var zipPath = CreateTestBackupZip();
        var service = new BackupImportService();

        await service.ImportBackupAsync(zipPath, _targetDbPath, _targetDataDir);

        var itemRepo = new ItemRepository(new SqliteConnection($"Data Source={_targetDbPath}"));
        // 需要打开连接
        using var conn = new SqliteConnection($"Data Source={_targetDbPath}");
        await conn.OpenAsync();
        var repo = new ItemRepository(conn);

        var item = await repo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(item);
        Assert.Equal("https://example.com", item!.OriginalUrl);
    }
}
