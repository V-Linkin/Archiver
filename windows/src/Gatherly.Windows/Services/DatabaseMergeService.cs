using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Services;

/// <summary>
/// 数据库合并服务 — 将备份中的数据库合并到目标数据库
/// 本阶段仅支持恢复到空数据库
/// </summary>
public class DatabaseMergeService
{
    private static readonly string[] CoreTables = new[]
    {
        "items", "media_assets", "folders", "trash_records",
        "import_tasks", "custom_platforms"
    };

    /// <summary>
    /// 将备份数据库合并到目标数据库
    /// 目标数据库必须为空，否则抛出异常
    /// </summary>
    public async Task MergeAsync(string backupDbPath, string targetDbPath)
    {
        // 确保目标数据库存在并执行 migration
        EnsureDatabaseReady(targetDbPath);

        // 检查目标数据库是否为空
        if (!IsDatabaseEmpty(targetDbPath))
        {
            throw new InvalidOperationException(
                "目标数据库不为空，本阶段仅支持恢复到空数据库。请先清空目标数据库或选择新的空数据库。");
        }

        using var connection = new SqliteConnection($"Data Source={targetDbPath}");
        await connection.OpenAsync();

        // ATTACH 备份数据库
        using (var attachCmd = connection.CreateCommand())
        {
            attachCmd.CommandText = $"ATTACH DATABASE '{backupDbPath}' AS backup_db";
            await attachCmd.ExecuteNonQueryAsync();
        }

        try
        {
            // 复制核心表数据
            foreach (var table in CoreTables)
            {
                await CopyTableDataAsync(connection, table);
            }
        }
        finally
        {
            // DETACH 备份数据库
            using (var detachCmd = connection.CreateCommand())
            {
                detachCmd.CommandText = "DETACH DATABASE backup_db";
                await detachCmd.ExecuteNonQueryAsync();
            }
        }

        // Rebuild FTS5 索引
        await RebuildFtsAsync(connection);
    }

    private void EnsureDatabaseReady(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        // 执行 migration
        Database.MigrationRunner.RunAll(connection);

        // 创建 custom_platforms 表（不在 migration 中，由 Repository 在运行时创建）
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS custom_platforms (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, logo_path TEXT,
                created_at REAL NOT NULL, sort_order INTEGER NOT NULL DEFAULT 0)";
            cmd.ExecuteNonQuery();
        }
    }

    private bool IsDatabaseEmpty(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        foreach (var table in CoreTables)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
            var count = Convert.ToInt64(cmd.ExecuteScalar());
            if (count > 0) return false;
        }

        return true;
    }

    private async Task CopyTableDataAsync(SqliteConnection connection, string table)
    {
        // 检查备份数据库中是否存在该表
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = $"SELECT COUNT(*) FROM backup_db.sqlite_master WHERE type='table' AND name='{table}'";
        var exists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
        if (exists == 0) return;

        // INSERT OR IGNORE 复制数据
        using var insertCmd = connection.CreateCommand();
        insertCmd.CommandText = $"INSERT OR IGNORE INTO {table} SELECT * FROM backup_db.{table}";
        await insertCmd.ExecuteNonQueryAsync();
    }

    private async Task RebuildFtsAsync(SqliteConnection connection)
    {
        // 检查 items_fts 表是否存在
        using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='items_fts'";
        var exists = Convert.ToInt64(await checkCmd.ExecuteScalarAsync());
        if (exists == 0) return;

        // Rebuild FTS 索引
        using var rebuildCmd = connection.CreateCommand();
        rebuildCmd.CommandText = "INSERT INTO items_fts(items_fts) VALUES('rebuild')";
        await rebuildCmd.ExecuteNonQueryAsync();
    }
}
