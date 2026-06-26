using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class MigrationBackupGateTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _testDir;
    private readonly CustomPlatformRepository _customPlatformRepo;
    private readonly SystemPlatformDisplayNames _displayNames;
    private readonly SystemPlatformCustomMap _customMap;

    public MigrationBackupGateTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS custom_platforms (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, logo_path TEXT,
                created_at REAL NOT NULL, sort_order INTEGER NOT NULL DEFAULT 0)";
            cmd.ExecuteNonQuery();
        }
        _testDir = Path.Combine(Path.GetTempPath(), "GatherlyMigrationTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _customPlatformRepo = new CustomPlatformRepository(_connection);
        _displayNames = new SystemPlatformDisplayNames(_testDir);
        _customMap = new SystemPlatformCustomMap(_testDir);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
        try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); }
        catch { try { if (File.Exists(_testDir + "/backups")) File.Delete(_testDir + "/backups"); Directory.Delete(_testDir, true); } catch { } }
    }

    private void InsertSystemItem(string platform)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT INTO items (id, title, body, original_url, platform, normalized_url, import_date, modify_date, content_status, archive_status, media_status) VALUES ($id, 'test', 'test', 'https://test.com', $p, 'https://test.com', 0, 0, 'normal', 'pending', 'textOnly')";
        cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        cmd.Parameters.AddWithValue("$p", platform);
        cmd.ExecuteNonQuery();
    }

    private int CountSystemOrphans()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE deleted_at IS NULL AND custom_platform_id IS NULL AND lower(platform) IN ('youtube','bilibili','github')";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    [Fact]
    public void NoWork_SkipsBackupAndMigration()
    {
        var migration = new SystemPlatformItemMigrationService(_connection, _customPlatformRepo, _customMap, _displayNames);
        var result = migration.Migrate();
        Assert.True(result.IsComplete);
        Assert.False(result.WasBlocked);
        Assert.Empty(result.MigratedItems);
    }

    [Fact]
    public void BackupSuccess_MigrationProceeds()
    {
        InsertSystemItem("youtube");
        InsertSystemItem("bilibili");
        Assert.Equal(2, CountSystemOrphans());

        var backupService = new MigrationBackupService(_connection, _testDir);
        var migration = new SystemPlatformItemMigrationService(_connection, _customPlatformRepo, _customMap, _displayNames, backupService);
        var result = migration.Migrate();

        Assert.True(result.IsComplete);
        Assert.False(result.WasBlocked);
        Assert.Equal(2, result.MigratedItems.Count);
        Assert.Equal(0, CountSystemOrphans());
    }

    private string GetBlockedPath()
    {
        // Create a file at the "backups" level to block subdirectory creation
        var backupsDir = Path.Combine(_testDir, "backups");
        File.WriteAllText(backupsDir, "blocked");
        return _testDir;
    }

    [Fact]
    public void BackupFailure_BlocksMigration()
    {
        InsertSystemItem("youtube");
        Assert.Equal(1, CountSystemOrphans());

        var badBackupService = new MigrationBackupService(_connection, GetBlockedPath());
        var migration = new SystemPlatformItemMigrationService(_connection, _customPlatformRepo, _customMap, _displayNames, badBackupService);
        var result = migration.Migrate();

        Assert.True(result.WasBlocked);
        Assert.False(result.IsComplete);
        Assert.Equal(1, CountSystemOrphans());
    }

    [Fact]
    public void BackupFailure_NoCustomPlatformCreated()
    {
        InsertSystemItem("github");
        var countBefore = _customPlatformRepo.GetAllAsync().GetAwaiter().GetResult().Count;

        var badBackupService = new MigrationBackupService(_connection, GetBlockedPath());
        var migration = new SystemPlatformItemMigrationService(_connection, _customPlatformRepo, _customMap, _displayNames, badBackupService);
        migration.Migrate();

        var countAfter = _customPlatformRepo.GetAllAsync().GetAwaiter().GetResult().Count;
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void BackupFailure_MappingNotWritten()
    {
        InsertSystemItem("youtube");
        var migration = new SystemPlatformItemMigrationService(_connection, _customPlatformRepo, _customMap, _displayNames,
            new MigrationBackupService(_connection, GetBlockedPath()));
        migration.Migrate();

        Assert.Null(_customMap.GetCustomPlatformId("youtube"));
    }

    [Fact]
    public void Plan_HasCorrectFields()
    {
        InsertSystemItem("youtube");
        var migration = new SystemPlatformItemMigrationService(_connection, _customPlatformRepo, _customMap, _displayNames);
        var plan = migration.BuildMigrationPlan();

        Assert.Single(plan.Items);
        Assert.Equal("youtube", plan.Items[0].SystemRawValue);
        Assert.Equal(1, plan.Items[0].SourceItemCount);
        Assert.Equal("YouTube", plan.Items[0].DefaultDisplayName);
        Assert.Equal(1, plan.TotalSourceItemCount);
    }

    [Fact]
    public void Plan_ZeroOrphans_ReturnsEmpty()
    {
        var migration = new SystemPlatformItemMigrationService(_connection, _customPlatformRepo, _customMap, _displayNames);
        var plan = migration.BuildMigrationPlan();
        Assert.Equal(0, plan.TotalSourceItemCount);
        Assert.Empty(plan.Items);
    }

    [Fact]
    public void Backup_VerifiesSnapshotReadable()
    {
        InsertSystemItem("youtube");
        var backupService = new MigrationBackupService(_connection, _testDir);
        var plan = new Dictionary<string, int> { ["youtube"] = 1 };
        var result = backupService.CreatePreMigrationBackupAsync(plan).GetAwaiter().GetResult();

        Assert.True(result.Success);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(Path.Combine(result.BackupPath!, "Gatherly.db")));
        Assert.True(File.Exists(Path.Combine(result.BackupPath!, "manifest.json")));
    }
}
