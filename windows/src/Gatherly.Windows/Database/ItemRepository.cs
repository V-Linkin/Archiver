using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// Item 数据访问层 — 读取 + 最小写入
/// </summary>
public class ItemRepository
{
    private readonly SqliteConnection _connection;

    public ItemRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    // ==================== Read ====================

    public async Task<List<Item>> GetRecentAsync(int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL ORDER BY import_date DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);
        return await ReadItemsAsync(cmd);
    }

    public async Task<Item?> GetByIdAsync(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqliteRowMapper.ReadItem(reader) : null;
    }

    public async Task<Item?> GetByNormalizedUrlAsync(string normalizedUrl)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE normalized_url=$url AND deleted_at IS NULL LIMIT 1";
        cmd.Parameters.AddWithValue("$url", normalizedUrl);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqliteRowMapper.ReadItem(reader) : null;
    }

    /// <summary>
    /// 获取 item 在数据库中的原始 id 字符串（保留大小写）
    /// </summary>
    public async Task<string?> GetRawIdAsync(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT id FROM items WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        return (string?)await cmd.ExecuteScalarAsync();
    }

    public async Task<List<Item>> GetByPlatformAsync(Platform platform, int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL AND platform=$platform ORDER BY import_date DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$platform", platform.ToRawValue());
        cmd.Parameters.AddWithValue("$limit", limit);
        return await ReadItemsAsync(cmd);
    }

    public async Task<List<Item>> GetByFolderIdAsync(Guid folderId, int limit = 100)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL AND folder_id COLLATE NOCASE=$folderId ORDER BY import_date DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$folderId", folderId.ToString("D"));
        cmd.Parameters.AddWithValue("$limit", limit);
        return await ReadItemsAsync(cmd);
    }

    public async Task<List<Item>> GetByCustomPlatformIdAsync(Guid customPlatformId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL AND custom_platform_id COLLATE NOCASE=$cpId ORDER BY import_date DESC";
        cmd.Parameters.AddWithValue("$cpId", customPlatformId.ToString("D"));
        return await ReadItemsAsync(cmd);
    }

    public async Task<List<Item>> GetUncategorizedItemsAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NULL AND platform=$platform AND custom_platform_id IS NULL ORDER BY import_date DESC";
        cmd.Parameters.AddWithValue("$platform", Platform.custom.ToRawValue());
        return await ReadItemsAsync(cmd);
    }

    public async Task<List<Item>> GetTrashedAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM items WHERE deleted_at IS NOT NULL ORDER BY deleted_at DESC";
        return await ReadItemsAsync(cmd);
    }

    /// <summary>
    /// 插入新 item
    /// </summary>
    public async Task InsertAsync(Item item)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, title, body, original_url, platform, platform_content_id,
                normalized_url, author, author_id, publish_date, import_date, modify_date,
                content_status, archive_status, media_status, cover_asset_id, folder_id,
                remark, is_starred, version, deleted_at, custom_platform_id)
            VALUES ($id, $title, $body, $originalUrl, $platform, $platformContentId,
                $normalizedUrl, $author, $authorId, $publishDate, $importDate, $modifyDate,
                $contentStatus, $archiveStatus, $mediaStatus, $coverAssetId, $folderId,
                $remark, $isStarred, $version, $deletedAt, $customPlatformId)";
        cmd.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        cmd.Parameters.AddWithValue("$title", (object?)item.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$body", (object?)item.Body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$originalUrl", item.OriginalUrl);
        cmd.Parameters.AddWithValue("$platform", item.Platform.ToRawValue());
        cmd.Parameters.AddWithValue("$platformContentId", (object?)item.PlatformContentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$normalizedUrl", item.NormalizedUrl);
        cmd.Parameters.AddWithValue("$author", (object?)item.Author ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$authorId", (object?)item.AuthorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$publishDate", item.PublishDate?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$importDate", item.ImportDate.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$modifyDate", item.ModifyDate.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$contentStatus", item.ContentStatus.ToRawValue());
        cmd.Parameters.AddWithValue("$archiveStatus", item.ArchiveStatus.ToRawValue());
        cmd.Parameters.AddWithValue("$mediaStatus", item.MediaStatus.ToRawValue());
        cmd.Parameters.AddWithValue("$coverAssetId", item.CoverAssetId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$folderId", item.FolderId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$remark", (object?)item.Remark ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$isStarred", item.IsStarred ? 1 : 0);
        cmd.Parameters.AddWithValue("$version", item.Version);
        cmd.Parameters.AddWithValue("$deletedAt", item.DeletedAt?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$customPlatformId", item.CustomPlatformId?.ToString() ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ==================== Write ====================

    /// <summary>
    /// 更新 item 全字段
    /// </summary>
    public async Task UpdateAsync(Item item)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE items SET
                title=$title, body=$body, original_url=$originalUrl, platform=$platform,
                platform_content_id=$platformContentId, normalized_url=$normalizedUrl,
                author=$author, author_id=$authorId,
                publish_date=$publishDate, import_date=$importDate, modify_date=$modifyDate,
                content_status=$contentStatus, archive_status=$archiveStatus,
                media_status=$mediaStatus, cover_asset_id=$coverAssetId,
                folder_id=$folderId, remark=$remark, is_starred=$isStarred,
                version=$version, deleted_at=$deletedAt, custom_platform_id=$customPlatformId
            WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", item.Id.ToString("D"));
        cmd.Parameters.AddWithValue("$title", (object?)item.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$body", (object?)item.Body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$originalUrl", item.OriginalUrl);
        cmd.Parameters.AddWithValue("$platform", item.Platform.ToRawValue());
        cmd.Parameters.AddWithValue("$platformContentId", (object?)item.PlatformContentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$normalizedUrl", item.NormalizedUrl);
        cmd.Parameters.AddWithValue("$author", (object?)item.Author ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$authorId", (object?)item.AuthorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$publishDate", item.PublishDate?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$importDate", item.ImportDate.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$modifyDate", item.ModifyDate.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$contentStatus", item.ContentStatus.ToRawValue());
        cmd.Parameters.AddWithValue("$archiveStatus", item.ArchiveStatus.ToRawValue());
        cmd.Parameters.AddWithValue("$mediaStatus", item.MediaStatus.ToRawValue());
        cmd.Parameters.AddWithValue("$coverAssetId", item.CoverAssetId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$folderId", item.FolderId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$remark", (object?)item.Remark ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$isStarred", item.IsStarred ? 1 : 0);
        cmd.Parameters.AddWithValue("$version", item.Version);
        cmd.Parameters.AddWithValue("$deletedAt", item.DeletedAt?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$customPlatformId", item.CustomPlatformId?.ToString() ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }


    // ==================== Write ====================

    /// <summary>
    /// 永久删除 item（依赖外键 cascade 删除 media_assets / trash_records）
    /// </summary>
    public async Task DeleteAsync(Guid itemId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", itemId.ToString("D"));
        await cmd.ExecuteNonQueryAsync();
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
