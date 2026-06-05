using Gatherly.Windows.Models;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// CustomPlatform 数据访问层 — 只读
/// </summary>
public class CustomPlatformRepository
{
    private readonly SqliteConnection _connection;

    public CustomPlatformRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<List<CustomPlatform>> GetAllAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM custom_platforms ORDER BY sort_order, created_at";
        using var reader = await cmd.ExecuteReaderAsync();
        var platforms = new List<CustomPlatform>();
        while (await reader.ReadAsync())
        {
            platforms.Add(SqliteRowMapper.ReadCustomPlatform(reader));
        }
        return platforms;
    }

    public async Task<CustomPlatform?> GetByIdAsync(Guid id)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM custom_platforms WHERE id=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqliteRowMapper.ReadCustomPlatform(reader) : null;
    }
}
