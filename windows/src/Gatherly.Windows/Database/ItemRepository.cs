using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// Item 数据访问层 — 只读
/// </summary>
public class ItemRepository
{
    private readonly SqliteConnection _connection;

    public ItemRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// 获取最近导入的内容（默认排除已删除）
    /// </summary>
    public async Task<List<Item>> GetRecentAsync(int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL ORDER BY import_date DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        return await ReadItemsAsync(cmd);
    }

    /// <summary>
    /// 根据 ID 获取 item
    /// </summary>
    public async Task<Item?> GetByIdAsync(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqliteRowMapper.ReadItem(reader) : null;
    }

    /// <summary>
    /// 按平台获取内容
    /// </summary>
    public async Task<List<Item>> GetByPlatformAsync(Platform platform, int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL AND platform=$platform ORDER BY import_date DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$platform", platform.ToRawValue());
        cmd.Parameters.AddWithValue("$limit", limit);
        return await ReadItemsAsync(cmd);
    }

    /// <summary>
    /// 按文件夹 ID 获取内容
    /// </summary>
    public async Task<List<Item>> GetByFolderIdAsync(Guid folderId, int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL AND folder_id=$folderId ORDER BY import_date DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$folderId", folderId.ToString());
        cmd.Parameters.AddWithValue("$limit", limit);
        return await ReadItemsAsync(cmd);
    }

    /// <summary>
    /// 按自定义平台 ID 获取内容
    /// </summary>
    public async Task<List<Item>> GetByCustomPlatformIdAsync(Guid customPlatformId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL AND custom_platform_id=$cpId ORDER BY import_date DESC";
        cmd.Parameters.AddWithValue("$cpId", customPlatformId.ToString());
        return await ReadItemsAsync(cmd);
    }

    /// <summary>
    /// 获取未分类内容（platform=custom 且 customPlatformId 为 null）
    /// </summary>
    public async Task<List<Item>> GetUncategorizedItemsAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL AND platform=$platform AND custom_platform_id IS NULL ORDER BY import_date DESC";
        cmd.Parameters.AddWithValue("$platform", Platform.custom.ToRawValue());
        return await ReadItemsAsync(cmd);
    }

    /// <summary>
    /// 获取回收站内容
    /// </summary>
    public async Task<List<Item>> GetTrashedAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NOT NULL ORDER BY deleted_at DESC";
        return await ReadItemsAsync(cmd);
    }

    private async Task<List<Item>> ReadItemsAsync(SqliteCommand cmd)
    {
        using var reader = await cmd.ExecuteReaderAsync();
        var items = new List<Item>();
        while (await reader.ReadAsync())
        {
            items.Add(SqliteRowMapper.ReadItem(reader));
        }
        return items;
    }
}
