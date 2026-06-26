using System.IO.Compression;
using System.Text.Json;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.Services.Backup;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class BackupPackageV2Tests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly string _testDir;

    public BackupPackageV2Tests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "GlyBakV2_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        var dbPath = Path.Combine(_testDir, "test.db");
        _conn = new SqliteConnection($"Data Source={dbPath}");
        _conn.Open();
        MigrationRunner.RunAll(_conn);
        using (var c = _conn.CreateCommand())
        {
            c.CommandText = @"CREATE TABLE IF NOT EXISTS custom_platforms (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, logo_path TEXT,
                created_at REAL NOT NULL, sort_order INTEGER NOT NULL DEFAULT 0)";
            c.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _conn.Close(); _conn.Dispose();
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private void InsertItem(string platform = "youtube", string? cpId = null)
    {
        using var c = _conn.CreateCommand();
        c.CommandText = @"INSERT INTO items (id, title, body, original_url, platform, normalized_url, import_date, modify_date, content_status, archive_status, media_status, custom_platform_id)
            VALUES ($id, 'test', 'body', 'https://x.com', $p, 'https://x.com', 0, 0, 'normal', 'pending', 'textOnly', $cid)";
        c.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        c.Parameters.AddWithValue("$p", platform);
        c.Parameters.AddWithValue("$cid", (object?)cpId ?? DBNull.Value);
        c.ExecuteNonQuery();
    }

    private BackupPackageV2Service CreateService() =>
        new(_conn, new CustomPlatformRepository(_conn), new SystemPlatformDisplayNames(_testDir), new SystemPlatformCustomMap(_testDir), _testDir);

    // === Manifest ===
    [Fact] public void ManifestFormatVersion_Is2() => Assert.Equal(2, new BackupPackageV2Manifest().FormatVersion);

    [Fact]
    public async Task CreateBackup_GeneratesValidZip()
    {
        InsertItem("youtube");
        var dest = Path.Combine(_testDir, "test.zip");
        var result = await CreateService().CreateBackupAsync(dest);
        if (!result.Success)
            Assert.Fail($"Backup failed: {result.ErrorMessage}");
        Assert.True(File.Exists(dest) && new FileInfo(dest).Length > 0);
    }

    [Fact]
    public async Task CreateBackup_ContainsManifestJson()
    {
        InsertItem();
        var dest = Path.Combine(_testDir, "test.zip");
        await CreateService().CreateBackupAsync(dest);

        using var archive = ZipFile.OpenRead(dest);
        using var stream = archive.GetEntry("manifest.json")!.Open();
        var doc = System.Text.Json.JsonDocument.Parse(stream);
        Assert.Equal(2, doc.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.True(doc.RootElement.TryGetProperty("createdAt", out _));
        Assert.True(doc.RootElement.TryGetProperty("databaseSchemaVersion", out _));
        Assert.True(doc.RootElement.TryGetProperty("databaseUserVersion", out _));
    }

    [Fact]
    public async Task CreateBackup_ContainsDatabase()
    {
        InsertItem();
        var dest = Path.Combine(_testDir, "test.zip");
        await CreateService().CreateBackupAsync(dest);
        using var archive = ZipFile.OpenRead(dest);
        Assert.NotNull(archive.GetEntry("database/archiver.db"));
    }

    [Fact]
    public async Task CreateBackup_ContainsSettingsFiles()
    {
        var svc = CreateService();
        var dest = Path.Combine(_testDir, "test.zip");
        await svc.CreateBackupAsync(dest);
        using var archive = ZipFile.OpenRead(dest);
        Assert.NotNull(archive.GetEntry("settings/platform_display_names.json"));
        Assert.NotNull(archive.GetEntry("settings/system_platform_mappings.json"));
    }

    [Fact]
    public async Task CreateBackup_EmptySettings_StillGeneratesFiles()
    {
        var svc = CreateService();
        var dest = Path.Combine(_testDir, "test.zip");
        await svc.CreateBackupAsync(dest);
        using var archive = ZipFile.OpenRead(dest);
        using var s = archive.GetEntry("settings/platform_display_names.json")!.Open();
        var doc = System.Text.Json.JsonDocument.Parse(s);
        Assert.True(doc.RootElement.TryGetProperty("platformDisplayNames", out var arr));
        Assert.Equal(0, arr.GetArrayLength());
    }

    // === Pending ===
    [Fact]
    public async Task CreateBackup_PendingBlocksBackup()
    {
        File.WriteAllText(Path.Combine(_testDir, "system_platform_custom_map.pending.json"),
            "{\"youtube\":\"00000000-0000-0000-0000-000000000000\"}");
        var dest = Path.Combine(_testDir, "test.zip");
        var result = await CreateService().CreateBackupAsync(dest);
        Assert.False(result.Success);
        Assert.Contains("pending", result.ErrorMessage!);
        Assert.False(File.Exists(dest));
    }

    // === Format Detector ===
    [Fact]
    public void FormatDetector_V2Detected()
    {
        CreateService().CreateBackupAsync(Path.Combine(_testDir, "test.zip")).GetAwaiter().GetResult();
        var (format, _) = BackupPackageFormatDetector.Detect(Path.Combine(_testDir, "test.zip"));
        Assert.Equal(BackupPackageFormat.V2, format);
    }

    [Fact]
    public void FormatDetector_EmptyZip_Unknown()
    {
        var zipPath = Path.Combine(_testDir, "empty.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create)) { }
        var (format, _) = BackupPackageFormatDetector.Detect(zipPath);
        Assert.Equal(BackupPackageFormat.Unknown, format);
    }

    [Fact]
    public void FormatDetector_NonExistent_Unknown()
    {
        var (format, _) = BackupPackageFormatDetector.Detect(Path.Combine(_testDir, "nope.zip"));
        Assert.Equal(BackupPackageFormat.Unknown, format);
    }

    // === Path Resolver ===
    [Fact]
    public void PathResolver_Normalizes()
    {
        Assert.Equal("media/test.jpg", BackupPathResolver.NormalizePackagePath("media/test.jpg"));
        Assert.Equal("media/test.jpg", BackupPathResolver.NormalizePackagePath("media\\test.jpg"));
    }

    [Fact]
    public void PathResolver_RejectsAbsolute()
    {
        Assert.Throws<ArgumentException>(() => BackupPathResolver.NormalizePackagePath("/etc/passwd"));
        Assert.Throws<ArgumentException>(() => BackupPathResolver.NormalizePackagePath("C:\\test"));
    }

    [Fact]
    public void PathResolver_RejectsTraversal()
    {
        Assert.Throws<ArgumentException>(() => BackupPathResolver.NormalizePackagePath("../etc/passwd"));
    }

    [Fact]
    public void PathResolver_Collision_SameHash()
    {
        // When hash matches, caller reuses original path without calling ResolveCollision
        // ResolveCollision always appends hash suffix
        var result = BackupPathResolver.ResolveCollision("media/a.jpg", "abc123");
        Assert.Contains("a_", result);
        Assert.Contains("abc123", result);
        Assert.Contains(".jpg", result);
    }

    [Fact]
    public void PathResolver_Collision_DifferentHash()
    {
        var result = BackupPathResolver.ResolveCollision("media/a.jpg", "abc123def456ghi");
        Assert.Contains("abc123def456", result);
        Assert.Contains("a_", result);
        Assert.Contains(".jpg", result);
    }

    // === Schema Validation ===
    [Fact]
    public async Task Manifest_PassesSchema()
    {
        InsertItem();
        var dest = Path.Combine(_testDir, "test.zip");
        await CreateService().CreateBackupAsync(dest);

        using var archive = ZipFile.OpenRead(dest);
        using var stream = archive.GetEntry("manifest.json")!.Open();
        var doc = System.Text.Json.JsonDocument.Parse(stream);
        var root = doc.RootElement;

        Assert.Equal(2, root.GetProperty("formatVersion").GetInt32());
        Assert.True(root.TryPropertyExists("createdAt"));
        Assert.True(root.TryPropertyExists("appVersion"));
        Assert.True(root.TryPropertyExists("sourceApp"));
        Assert.True(root.TryPropertyExists("sourceOS"));
        Assert.True(root.TryPropertyExists("databaseSchemaVersion"));
        Assert.True(root.TryPropertyExists("databaseUserVersion"));
        Assert.True(root.TryPropertyExists("counts"));
        Assert.True(root.TryPropertyExists("files"));
        Assert.True(root.TryPropertyExists("features"));
        Assert.True(root.TryPropertyExists("warnings"));
    }

    [Fact]
    public async Task Manifest_FilesSortedByPath()
    {
        InsertItem();
        var dest = Path.Combine(_testDir, "test.zip");
        await CreateService().CreateBackupAsync(dest);

        using var archive = ZipFile.OpenRead(dest);
        using var stream = archive.GetEntry("manifest.json")!.Open();
        var doc = System.Text.Json.JsonDocument.Parse(stream);
        var files = doc.RootElement.GetProperty("files").EnumerateArray().Select(f => f.GetProperty("path").GetString()!).ToList();
        Assert.Equal(files, files.OrderBy(x => x, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public async Task Manifest_AllHashesAreLowercase64Hex()
    {
        InsertItem();
        var dest = Path.Combine(_testDir, "test.zip");
        await CreateService().CreateBackupAsync(dest);

        using var archive = ZipFile.OpenRead(dest);
        using var stream = archive.GetEntry("manifest.json")!.Open();
        var doc = System.Text.Json.JsonDocument.Parse(stream);
        foreach (var f in doc.RootElement.GetProperty("files").EnumerateArray())
        {
            var hash = f.GetProperty("sha256").GetString()!;
            Assert.Equal(64, hash.Length);
            Assert.Matches("^[0-9a-f]+$", hash);
        }
    }

    // === ZIP Verification ===
    [Fact]
    public async Task CreateBackup_NoWarningsForCleanData()
    {
        InsertItem();
        var dest = Path.Combine(_testDir, "test.zip");
        await CreateService().CreateBackupAsync(dest);
        using var archive = ZipFile.OpenRead(dest);
        using var stream = archive.GetEntry("manifest.json")!.Open();
        var doc = System.Text.Json.JsonDocument.Parse(stream);
        Assert.Equal(0, doc.RootElement.GetProperty("warnings").GetArrayLength());
    }

    [Fact]
    public async Task CreateBackup_NoWalOrShm()
    {
        InsertItem();
        var dest = Path.Combine(_testDir, "test.zip");
        await CreateService().CreateBackupAsync(dest);
        using var archive = ZipFile.OpenRead(dest);
        Assert.Null(archive.GetEntry("database/archiver.db-wal"));
        Assert.Null(archive.GetEntry("database/archiver.db-shm"));
    }

    [Fact]
    public async Task CreateBackup_SourceDatabaseUnchanged()
    {
        InsertItem("youtube");
        var svc = CreateService();
        var countBefore = 0;
        using (var cmd = _conn.CreateCommand()) { cmd.CommandText = "SELECT COUNT(*) FROM items"; countBefore = Convert.ToInt32(cmd.ExecuteScalar()); }
        await svc.CreateBackupAsync(Path.Combine(_testDir, "test.zip"));
        var countAfter = 0;
        using (var cmd = _conn.CreateCommand()) { cmd.CommandText = "SELECT COUNT(*) FROM items"; countAfter = Convert.ToInt32(cmd.ExecuteScalar()); }
        Assert.Equal(countBefore, countAfter);
    }

    // === Cancellation ===
    [Fact]
    public async Task CreateBackup_CancellationCleansUp()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var dest = Path.Combine(_testDir, "test.zip");
        var result = await CreateService().CreateBackupAsync(dest, ct: cts.Token);
        Assert.False(result.Success);
        Assert.False(File.Exists(dest));
    }

    // === Target Safety ===
    [Fact]
    public async Task CreateBackup_TargetNotExist_CreatesFile()
    {
        var dest = Path.Combine(_testDir, "new_backup.zip");
        Assert.False(File.Exists(dest));
        var result = await CreateService().CreateBackupAsync(dest);
        Assert.True(result.Success);
        Assert.True(File.Exists(dest));
    }
}

internal static class JsonElementExtensions
{
    public static bool TryPropertyExists(this JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out _);
}
