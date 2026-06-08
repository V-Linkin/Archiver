using System;
using System.IO;
using Gatherly.Windows.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class DatabaseMigrationTests : IDisposable
{
    private readonly string _tempDbPath;

    public DatabaseMigrationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"gatherly_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        System.Threading.Thread.Sleep(300);

        // Retry file deletion to handle Windows WAL lock release delay
        for (int retry = 0; retry < 5; retry++)
        {
            try
            {
                if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
                var walPath = _tempDbPath + "-wal";
                var shmPath = _tempDbPath + "-shm";
                if (File.Exists(walPath)) File.Delete(walPath);
                if (File.Exists(shmPath)) File.Delete(shmPath);
                break;
            }
            catch (IOException) when (retry < 4)
            {
                System.Threading.Thread.Sleep(300);
            }
        }
    }

    [Fact]
    public void Initialize_CreatesDatabaseFile()
    {
        using var connection = DatabaseInitializer.Initialize(_tempDbPath);
        Assert.True(File.Exists(_tempDbPath));
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [Fact]
    public void Initialize_CreatesCoreTables()
    {
        using var connection = DatabaseInitializer.Initialize(_tempDbPath);
        
        var tables = GetTableNames(connection);
        
        Assert.Contains("items", tables);
        Assert.Contains("media_assets", tables);
        Assert.Contains("folders", tables);
        Assert.Contains("trash_records", tables);
        Assert.Contains("import_tasks", tables);
    }

    [Fact]
    public void Initialize_CreatesFTSTable()
    {
        using var connection = DatabaseInitializer.Initialize(_tempDbPath);
        
        var tables = GetTableNames(connection);
        Assert.Contains("items_fts", tables);
    }

    [Fact]
    public void Initialize_CreatesIndexes()
    {
        using var connection = DatabaseInitializer.Initialize(_tempDbPath);
        
        var indexes = GetIndexNames(connection);
        
        Assert.Contains("idx_items_platform", indexes);
        Assert.Contains("idx_items_normalized_url", indexes);
        Assert.Contains("idx_media_item", indexes);
        Assert.Contains("idx_folders_platform", indexes);
        Assert.Contains("idx_trash_deleted_at", indexes);
        Assert.Contains("idx_tasks_status", indexes);
    }

    [Fact]
    public void Initialize_IsIdempotent()
    {
        // 第一次初始化
        using (var conn1 = DatabaseInitializer.Initialize(_tempDbPath))
        {
            var tables1 = GetTableNames(conn1);
            Assert.Contains("items", tables1);
        }

        // 第二次初始化不崩溃
        using (var conn2 = DatabaseInitializer.Initialize(_tempDbPath))
        {
            var tables2 = GetTableNames(conn2);
            Assert.Contains("items", tables2);
            Assert.Contains("items_fts", tables2);
        }
    }

    [Fact]
    public void Initialize_SetsWALMode()
    {
        using var connection = DatabaseInitializer.Initialize(_tempDbPath);
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode;";
        var mode = cmd.ExecuteScalar()?.ToString();
        Assert.Equal("wal", mode);
    }

    [Fact]
    public void Initialize_FTS5_CanInsertAndQuery()
    {
        using var connection = DatabaseInitializer.Initialize(_tempDbPath);

        // 插入测试数据
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO items (id, title, body, original_url, platform, normalized_url, import_date, modify_date, content_status, archive_status, media_status) VALUES ('test-1', 'hello world', 'test content', 'https://example.com', 'bilibili', 'https://example.com', 1700000000, 1700000000, 'normal', 'pending', 'textOnly')";
            cmd.ExecuteNonQuery();
        }

        // 获取 rowid 并插入 FTS
        long rowid;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT rowid FROM items WHERE id='test-1'";
            rowid = (long)cmd.ExecuteScalar()!;
        }
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"INSERT INTO items_fts (rowid, title, body) VALUES ({rowid}, 'hello world', 'test content')";
            cmd.ExecuteNonQuery();
        }

        // FTS 搜索 - 英文关键词验证 FTS5 基本功能
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM items_fts WHERE items_fts MATCH 'hello'";
            var count = Convert.ToInt64(cmd.ExecuteScalar()!);
            Assert.Equal(1, count);
        }

        // 验证 FTS 索引和 items 表 rowid 关联
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT items.title FROM items JOIN items_fts ON items.rowid = items_fts.rowid WHERE items_fts MATCH 'hello'";
            var title = cmd.ExecuteScalar()?.ToString();
            Assert.Equal("hello world", title);
        }
    }

    private static List<string> GetTableNames(SqliteConnection connection)
    {
        var tables = new List<string>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }
        return tables;
    }

    private static List<string> GetIndexNames(SqliteConnection connection)
    {
        var indexes = new List<string>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name LIKE 'idx_%' ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            indexes.Add(reader.GetString(0));
        }
        return indexes;
    }
}
