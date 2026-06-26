using Gatherly.Windows.Models;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// CustomPlatform 数据访问层
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
        cmd.CommandText = "SELECT * FROM custom_platforms WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqliteRowMapper.ReadCustomPlatform(reader) : null;
    }

    public async Task<CustomPlatform?> GetByNameAsync(string name)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM custom_platforms WHERE lower(trim(name))=lower(trim($name))";
        cmd.Parameters.AddWithValue("$name", name);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? SqliteRowMapper.ReadCustomPlatform(reader) : null;
    }

    public async Task<CustomPlatform> CreateAsync(string name, string? logoPath = null, int? sortOrder = null)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrEmpty(trimmedName))
            throw new ArgumentException("平台名称不能为空");

        // 获取下一个 sort_order
        var maxSort = await GetMaxSortOrderAsync();
        var actualSortOrder = sortOrder ?? (maxSort + 1);

        var platform = new CustomPlatform
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            LogoPath = string.IsNullOrWhiteSpace(logoPath) ? null : logoPath,
            CreatedAt = DateTimeOffset.UtcNow,
            SortOrder = actualSortOrder
        };

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO custom_platforms (id, name, logo_path, created_at, sort_order)
            VALUES ($id, $name, $logoPath, $createdAt, $sortOrder)";
        cmd.Parameters.AddWithValue("$id", platform.Id.ToString("D"));
        cmd.Parameters.AddWithValue("$name", platform.Name);
        cmd.Parameters.AddWithValue("$logoPath", (object?)platform.LogoPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$createdAt", platform.CreatedAt.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$sortOrder", platform.SortOrder);
        await cmd.ExecuteNonQueryAsync();

        return platform;
    }

    public async Task<CustomPlatform?> UpdateAsync(Guid id, string? name = null, string? logoPath = null, int? sortOrder = null)
    {
        var existing = await GetByIdAsync(id);
        if (existing == null)
            return null;

        var updates = new List<string>();
        var cmd = _connection.CreateCommand();

        if (name != null)
        {
            var trimmedName = name.Trim();
            if (string.IsNullOrEmpty(trimmedName))
                throw new ArgumentException("平台名称不能为空");
            updates.Add("name=$name");
            cmd.Parameters.AddWithValue("$name", trimmedName);
        }

        if (logoPath != null)
        {
            updates.Add("logo_path=$logoPath");
            cmd.Parameters.AddWithValue("$logoPath", logoPath);
        }
        else if (logoPath == null)
        {
            updates.Add("logo_path=NULL");
        }

        if (sortOrder.HasValue)
        {
            updates.Add("sort_order=$sortOrder");
            cmd.Parameters.AddWithValue("$sortOrder", sortOrder.Value);
        }

        if (updates.Count == 0)
            return existing;

        cmd.CommandText = $"UPDATE custom_platforms SET {string.Join(", ", updates)} WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", id.ToString("D"));
        await cmd.ExecuteNonQueryAsync();

        return await GetByIdAsync(id);
    }

    public async Task<DeletePlatformResult> DeleteAsync(Guid id)
    {
        var platform = await GetByIdAsync(id);
        if (platform == null)
            return new DeletePlatformResult { Success = false, PlatformNotFound = true };

        var affectedCount = await CountItemsByPlatformIdAsync(id);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var transaction = _connection.BeginTransaction();
        try
        {
            // 1. 迁移关联 items 到未分类
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "UPDATE items SET platform='custom', custom_platform_id=NULL, modify_date=$now WHERE custom_platform_id COLLATE NOCASE=$id";
                cmd.Parameters.AddWithValue("$id", id.ToString("D"));
                cmd.Parameters.AddWithValue("$now", now);
                cmd.ExecuteNonQuery();
            }

            // 2. 删除平台记录
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM custom_platforms WHERE id COLLATE NOCASE=$id";
                cmd.Parameters.AddWithValue("$id", id.ToString("D"));
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();

            return new DeletePlatformResult
            {
                Success = true,
                DeletedPlatformId = id,
                DeletedPlatformName = platform.Name,
                AffectedItemCount = affectedCount
            };
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            transaction.Dispose();
        }
    }

    private async Task<int> CountItemsByPlatformIdAsync(Guid platformId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE custom_platform_id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", platformId.ToString("D"));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private async Task<int> GetMaxSortOrderAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(sort_order), 0) FROM custom_platforms";
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}

/// <summary>
/// 删除平台结果
/// </summary>
public class DeletePlatformResult
{
    public bool Success { get; set; }
    public bool PlatformNotFound { get; set; }
    public Guid DeletedPlatformId { get; set; }
    public string DeletedPlatformName { get; set; } = "";
    public int AffectedItemCount { get; set; }
}
