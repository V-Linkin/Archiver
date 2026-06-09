using Gatherly.Windows.Models;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// MediaAsset 数据访问层 — 只读
/// </summary>
public class MediaRepository
{
    private readonly SqliteConnection _connection;

    public MediaRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<MediaAsset>> GetByItemIdAsync(Guid itemId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM media_assets WHERE item_id COLLATE NOCASE=$itemId ORDER BY created_at";
        cmd.Parameters.AddWithValue("$itemId", itemId.ToString("D"));
        using var reader = await cmd.ExecuteReaderAsync();
        var assets = new List<MediaAsset>();
        while (await reader.ReadAsync())
        {
            assets.Add(SqliteRowMapper.ReadMediaAsset(reader));
        }
        return assets;
    }
}
