using System;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// SQLite 数据库初始化器
/// 创建或打开数据库，执行 migration
/// 可重复调用，依赖 SQL 中的 IF NOT EXISTS 保证幂等
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// 使用默认数据库路径初始化
    /// </summary>
    public static SqliteConnection Initialize()
    {
        return Initialize(DatabasePaths.DatabaseFile);
    }

    /// <summary>
    /// 使用指定路径初始化数据库
    /// </summary>
    public static SqliteConnection Initialize(string databasePath)
    {
        // 确保目录存在
        var dir = System.IO.Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
        {
            System.IO.Directory.CreateDirectory(dir);
        }

        var connectionString = $"Data Source={databasePath}";
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        // 启用 WAL 模式（与 macOS GRDB 兼容）
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
        }

        // 启用外键约束
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
        }

        // 执行 migration
        MigrationRunner.RunAll(connection);

        // 重建 FTS 索引（确保所有 item 都有 FTS 记录）
        RebuildFts(connection);

        return connection;
    }

    /// <summary>
    /// 重建 FTS 索引 — 确保所有 item 都有 FTS 记录
    /// </summary>
    private static void RebuildFts(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items_fts(rowid, title, body)
            SELECT rowid, title, body FROM items
            WHERE rowid NOT IN (SELECT rowid FROM items_fts)";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 使用临时数据库初始化（用于测试）
    /// </summary>
    public static SqliteConnection InitializeForTest()
    {
        var tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"gatherly_test_{Guid.NewGuid():N}.db");
        return Initialize(tempPath);
    }
}
