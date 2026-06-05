using Gatherly.Windows.Models;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// TrashRecord 数据访问层 — 只读
/// </summary>
public class TrashRepository
{
    private readonly SqliteConnection _connection;

    public TrashRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

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
        cmd.CommandText = "SELECT * FROM trash_records WHERE item_id=$itemId";
        cmd.Parameters.AddWithValue("$itemId", itemId.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqliteRowMapper.ReadTrashRecord(reader) : null;
    }
}
