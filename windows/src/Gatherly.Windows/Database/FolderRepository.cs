using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// Folder 数据访问层 — 只读
/// </summary>
public class FolderRepository
{
    private readonly SqliteConnection _connection;

    public FolderRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<Folder?> GetByIdAsync(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM folders WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqliteRowMapper.ReadFolder(reader) : null;
    }

    /// <summary>
    /// 检查 folder 是否存在（大小写精确匹配）
    /// </summary>
    public async Task<bool> ExistsAsync(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM folders WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0;
    }

    public async Task<List<Folder>> GetByPlatformAsync(Platform platform)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM folders WHERE platform=$platform AND parent_id IS NULL ORDER BY sort_order, name";
        cmd.Parameters.AddWithValue("$platform", platform.ToRawValue());
        return await ReadFoldersAsync(cmd);
    }

    public async Task<List<Folder>> GetByParentIdAsync(Guid parentId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM folders WHERE parent_id COLLATE NOCASE=$parentId ORDER BY sort_order, name";
        cmd.Parameters.AddWithValue("$parentId", parentId.ToString("D"));
        return await ReadFoldersAsync(cmd);
    }

    public async Task<List<Folder>> GetByCustomPlatformIdAsync(Guid customPlatformId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM folders WHERE platform=$platform AND custom_platform_id COLLATE NOCASE=$cpId AND parent_id IS NULL ORDER BY sort_order, name";
        cmd.Parameters.AddWithValue("$platform", Platform.custom.ToRawValue());
        cmd.Parameters.AddWithValue("$cpId", customPlatformId.ToString("D"));
        return await ReadFoldersAsync(cmd);
    }

    public async Task<List<Folder>> GetUncategorizedFoldersAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM folders WHERE platform=$platform AND custom_platform_id IS NULL AND parent_id IS NULL ORDER BY sort_order, name";
        cmd.Parameters.AddWithValue("$platform", Platform.custom.ToRawValue());
        return await ReadFoldersAsync(cmd);
    }

    public async Task<Folder> CreateAsync(string name, Platform platform, Guid? parentPlatformId = null,
        Guid? customPlatformId = null, int sortOrder = 0)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"INSERT INTO folders (id, name, parent_id, platform, created_at, sort_order, custom_platform_id)
            VALUES ($id, $name, $parentId, $platform, $createdAt, $sortOrder, $cpId)";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        cmd.Parameters.AddWithValue("$name", name.Trim());
        cmd.Parameters.AddWithValue("$parentId", parentPlatformId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$platform", platform.ToRawValue());
        cmd.Parameters.AddWithValue("$createdAt", now.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$sortOrder", sortOrder);
        cmd.Parameters.AddWithValue("$cpId", customPlatformId?.ToString() ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
        return (await GetByIdAsync(id))!;
    }

    public async Task UpdateNameAsync(Guid id, string newName)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE folders SET name=$name WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$name", newName.Trim());
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM folders WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> CountItemsAsync(Guid folderId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE folder_id COLLATE NOCASE=$folderId AND deleted_at IS NULL";
        cmd.Parameters.AddWithValue("$folderId", folderId.ToString("D"));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<List<Folder>> ReadFoldersAsync(SqliteCommand cmd)
    {
        using var reader = await cmd.ExecuteReaderAsync();
        var folders = new List<Folder>();
        while (await reader.ReadAsync())
        {
            folders.Add(SqliteRowMapper.ReadFolder(reader));
        }
        return folders;
    }
}
