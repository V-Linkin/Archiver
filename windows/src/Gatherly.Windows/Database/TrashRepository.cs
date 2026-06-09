using System.Text.Json;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// TrashRecord 数据访问层 — 读取 + 写入
/// </summary>
public class TrashRepository
{
    private readonly SqliteConnection _connection;

    public TrashRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    // ==================== Read ====================

    public async Task<List<TrashRecord>> GetAllAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM trash_records ORDER BY deleted_at DESC";
        using var reader = await cmd.ExecuteReaderAsync();
        var records = new List<TrashRecord>();
        while (await reader.ReadAsync())
        {
            records.Add(SqliteRowMapper.ReadTrashRecord(reader));
        }
        return records;
    }

    public async Task<TrashRecord?> GetByItemIdAsync(Guid itemId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM trash_records WHERE item_id COLLATE NOCASE=$itemId";
        cmd.Parameters.AddWithValue("$itemId", itemId.ToString("D"));
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqliteRowMapper.ReadTrashRecord(reader) : null;
    }

    // ==================== Write ====================

    public async Task InsertAsync(TrashRecord record)
    {
        var mediaPathsJson = JsonSerializer.Serialize(record.MediaPaths);

        // Use raw item_id from database to match FK case exactly
        var itemIdStr = record.RawItemId ?? record.ItemId.ToString();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO trash_records (id, item_id, deleted_at, auto_delete_at,
                original_folder_id, original_archive_status, media_paths)
            VALUES ($id, $itemId, $deletedAt, $autoDeleteAt,
                $originalFolderId, $originalArchiveStatus, $mediaPaths)";
        cmd.Parameters.AddWithValue("$id", record.Id.ToString());
        cmd.Parameters.AddWithValue("$itemId", itemIdStr);
        cmd.Parameters.AddWithValue("$deletedAt", record.DeletedAt.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$autoDeleteAt", record.AutoDeleteAt.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$originalFolderId", record.OriginalFolderId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$originalArchiveStatus", record.OriginalArchiveStatus.ToRawValue());
        cmd.Parameters.AddWithValue("$mediaPaths", mediaPathsJson);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteByItemIdAsync(Guid itemId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM trash_records WHERE item_id COLLATE NOCASE=$itemId";
        cmd.Parameters.AddWithValue("$itemId", itemId.ToString("D"));
        await cmd.ExecuteNonQueryAsync();
    }
}
