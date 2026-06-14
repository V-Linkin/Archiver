using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Database;

/// <summary>
/// ImportTask 数据访问层
/// </summary>
public class ImportTaskRepository
{
    private readonly SqliteConnection _connection;

    public ImportTaskRepository(SqliteConnection connection)
    {
        _connection = connection;
    }

    public async Task<ImportTask?> GetByNormalizedUrlAsync(string normalizedUrl)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM import_tasks WHERE normalized_url=$url LIMIT 1";
        cmd.Parameters.AddWithValue("$url", normalizedUrl);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadImportTask(reader) : null;
    }

    /// <summary>
    /// 获取指定 URL 的所有导入任务
    /// </summary>
    public async Task<List<ImportTask>> GetAllByNormalizedUrlAsync(string normalizedUrl)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM import_tasks WHERE normalized_url=$url ORDER BY updated_at DESC";
        cmd.Parameters.AddWithValue("$url", normalizedUrl);
        using var reader = await cmd.ExecuteReaderAsync();
        var tasks = new List<ImportTask>();
        while (await reader.ReadAsync())
        {
            tasks.Add(ReadImportTask(reader));
        }
        return tasks;
    }

    public async Task<ImportTask?> GetByOriginalUrlAsync(string originalUrl)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM import_tasks WHERE original_url=$url LIMIT 1";
        cmd.Parameters.AddWithValue("$url", originalUrl);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadImportTask(reader) : null;
    }

    public async Task<ImportTask> InsertAsync(ImportTask task)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, error_message, item_id, created_at, completed_at, updated_at, retry_count)
            VALUES ($id, $originalUrl, $normalizedUrl, $platform, $status, $progress, $errorMessage, $itemId, $createdAt, $completedAt, $updatedAt, $retryCount)";
        cmd.Parameters.AddWithValue("$id", task.Id.ToString("D"));
        cmd.Parameters.AddWithValue("$originalUrl", task.OriginalUrl);
        cmd.Parameters.AddWithValue("$normalizedUrl", task.NormalizedUrl);
        cmd.Parameters.AddWithValue("$platform", (object?)task.Platform?.ToRawValue() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$status", task.Status.ToRawValue());
        cmd.Parameters.AddWithValue("$progress", task.Progress);
        cmd.Parameters.AddWithValue("$errorMessage", (object?)task.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$itemId", task.ItemId?.ToString() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$createdAt", task.CreatedAt.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$completedAt", task.CompletedAt?.ToUnixTimeSeconds() ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$updatedAt", task.UpdatedAt.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$retryCount", task.RetryCount);
        await cmd.ExecuteNonQueryAsync();
        return task;
    }

    public async Task UpdateStatusAsync(Guid taskId, Models.Enums.TaskStatus status, string? errorMessage = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE import_tasks SET status=$status, error_message=$errorMessage, updated_at=$updatedAt WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", taskId.ToString("D"));
        cmd.Parameters.AddWithValue("$status", status.ToRawValue());
        cmd.Parameters.AddWithValue("$errorMessage", (object?)errorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateCompletedAsync(Guid taskId, Guid itemId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE import_tasks SET status=$status, item_id=$itemId, completed_at=$completedAt, updated_at=$updatedAt WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", taskId.ToString("D"));
        cmd.Parameters.AddWithValue("$status", Models.Enums.TaskStatus.completed.ToRawValue());
        cmd.Parameters.AddWithValue("$itemId", itemId.ToString("D"));
        cmd.Parameters.AddWithValue("$completedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await cmd.ExecuteNonQueryAsync();
    }

    private static ImportTask ReadImportTask(SqliteDataReader reader)
    {
        return new ImportTask
        {
            Id = Guid.Parse(reader.GetString(reader.GetOrdinal("id"))),
            OriginalUrl = reader.GetString(reader.GetOrdinal("original_url")),
            NormalizedUrl = reader.GetString(reader.GetOrdinal("normalized_url")),
            Platform = reader.IsDBNull(reader.GetOrdinal("platform"))
                ? null
                : PlatformExtensions.FromRawValue(reader.GetString(reader.GetOrdinal("platform"))),
            Status = Models.Enums.TaskStatusExtensions.FromRawValue(reader.GetString(reader.GetOrdinal("status"))),
            Progress = reader.GetDouble(reader.GetOrdinal("progress")),
            ErrorMessage = reader.IsDBNull(reader.GetOrdinal("error_message"))
                ? null
                : reader.GetString(reader.GetOrdinal("error_message")),
            ItemId = reader.IsDBNull(reader.GetOrdinal("item_id"))
                ? null
                : Guid.Parse(reader.GetString(reader.GetOrdinal("item_id"))),
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("created_at"))),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at"))
                ? null
                : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("completed_at"))),
            UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at"))
                ? DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("created_at")))
                : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reader.GetOrdinal("updated_at"))),
            RetryCount = reader.GetInt32(reader.GetOrdinal("retry_count"))
        };
    }
}
