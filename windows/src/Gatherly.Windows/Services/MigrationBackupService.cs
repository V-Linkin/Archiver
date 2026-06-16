using System.Text.Json;
using Gatherly.Windows.Database;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Services;

/// <summary>
/// 迁移前安全备份服务
/// </summary>
public class MigrationBackupService
{
    private readonly SqliteConnection _connection;
    private readonly string _dataDirectory;

    public MigrationBackupService(SqliteConnection connection, string? dataDirectory = null)
    {
        _connection = connection;
        _dataDirectory = dataDirectory ?? DatabasePaths.DataDirectory;
    }

    public async Task<MigrationBackupResult> CreatePreMigrationBackupAsync(
        Dictionary<string, int> migrationPlan)
    {
        if (migrationPlan.Count == 0)
            return new MigrationBackupResult(true, null, null);

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupDir = Path.Combine(_dataDirectory, "backups", $"pre-migration_{timestamp}");
            Directory.CreateDirectory(backupDir);

            // 1. SQLite VACUUM INTO for safe snapshot
            var dbSnapshot = Path.Combine(backupDir, "Gatherly.db");
            await using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = $"VACUUM INTO '{dbSnapshot.Replace("'", "''")}'";
                cmd.ExecuteNonQuery();
            }

            // 2. Copy media
            var mediaSrc = Path.Combine(_dataDirectory, "media");
            if (Directory.Exists(mediaSrc))
                CopyDirectory(mediaSrc, Path.Combine(backupDir, "media"));

            // 3. Copy platform_logos
            var logoSrc = Path.Combine(_dataDirectory, "platform_logos");
            if (Directory.Exists(logoSrc))
                CopyDirectory(logoSrc, Path.Combine(backupDir, "platform_logos"));

            // 4. Copy mapping files
            CopyIfExists(Path.Combine(_dataDirectory, "platform_display_names.json"), backupDir);
            CopyIfExists(Path.Combine(_dataDirectory, "system_platform_custom_map.json"), backupDir);

            // 5. Verify snapshot is readable
            var verifyResult = VerifyBackup(dbSnapshot);
            if (!verifyResult.Success)
            {
                Directory.Delete(backupDir, true);
                return new MigrationBackupResult(false, null, $"备份验证失败: {verifyResult.ErrorMessage}");
            }

            // 6. Write manifest
            var manifest = new
            {
                backupType = "pre-system-platform-migration",
                createdAt = DateTime.UtcNow.ToString("o"),
                sourceDatabasePath = Path.Combine(_dataDirectory, "Gatherly.db"),
                databaseSnapshot = "Gatherly.db",
                mediaIncluded = Directory.Exists(Path.Combine(backupDir, "media")),
                platformLogosIncluded = Directory.Exists(Path.Combine(backupDir, "platform_logos")),
                migrationPlan = migrationPlan,
                totalItemsToMigrate = migrationPlan.Values.Sum()
            };
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(backupDir, "manifest.json"), manifestJson);

            return new MigrationBackupResult(true, backupDir, null);
        }
        catch (Exception ex)
        {
            return new MigrationBackupResult(false, null, $"备份创建失败: {ex.Message}");
        }
    }

    private static (bool Success, string? ErrorMessage) VerifyBackup(string dbPath)
    {
        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            var result = cmd.ExecuteScalar()?.ToString();
            if (result != "ok")
                return (false, $"integrity_check 返回: {result}");
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"无法打开备份: {ex.Message}");
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }

    private static void CopyIfExists(string source, string destDir)
    {
        if (File.Exists(source))
            File.Copy(source, Path.Combine(destDir, Path.GetFileName(source)), true);
    }
}

public sealed record MigrationBackupResult(
    bool Success,
    string? BackupPath,
    string? ErrorMessage);
