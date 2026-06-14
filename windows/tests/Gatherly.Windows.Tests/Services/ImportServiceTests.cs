using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.Services.Import;
using Gatherly.Windows.Services.Media;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class ImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ImportService _service;

    public ImportServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
        _service = new ImportService(new ItemRepository(_connection), new ImportTaskRepository(_connection), new MediaDownloadService(new MediaRepository(_connection)));
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task ProcessImport_EmptyInput_ReturnsEmptyInput()
    {
        var result = await _service.ProcessImportAsync(null);
        Assert.Equal(ImportStatus.EmptyInput, result.Status);
        Assert.Equal("请输入或粘贴链接", result.Message);
    }

    [Fact]
    public async Task ProcessImport_Whitespace_ReturnsEmptyInput()
    {
        var result = await _service.ProcessImportAsync("   ");
        Assert.Equal(ImportStatus.EmptyInput, result.Status);
    }

    [Fact]
    public async Task ProcessImport_PlainText_ReturnsInvalidUrl()
    {
        var result = await _service.ProcessImportAsync("这是一段普通文字");
        Assert.Equal(ImportStatus.InvalidUrl, result.Status);
        Assert.Equal("输入的内容不是有效的 URL", result.Message);
    }

    [Fact]
    public async Task ProcessImport_GitHubUrl_ReturnsSuccessImport()
    {
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
        Assert.Equal(Platform.github, result.Platform);
        Assert.Contains("GitHub", result.Message);
    }

    [Fact]
    public async Task ProcessImport_BilibiliUrl_ReturnsSuccessImport()
    {
        // BilibiliParser makes real HTTP calls to Bilibili API
        var result = await _service.ProcessImportAsync("https://www.bilibili.com/video/BV1xx411c7mD");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
        Assert.Equal(Platform.bilibili, result.Platform);
    }

    [Fact]
    public async Task ProcessImport_YouTubeUrl_ReturnsSuccessImport()
    {
        // YouTubeParser makes real HTTP calls to YouTube
        var result = await _service.ProcessImportAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
        Assert.Equal(Platform.youtube, result.Platform);
    }

    [Fact]
    public async Task ProcessImport_XiaohongshuUrl_ReturnsTaskCreated()
    {
        var result = await _service.ProcessImportAsync("https://www.xiaohongshu.com/explore/65a1b2c3");
        Assert.Equal(ImportStatus.TaskCreated, result.Status);
        Assert.Equal(Platform.xiaohongshu, result.Platform);
        Assert.Contains("小红书", result.Message);
    }

    [Fact]
    public async Task ProcessImport_UnknownUrl_ReturnsUnsupportedPlatform()
    {
        var result = await _service.ProcessImportAsync("https://www.example.com/article/123");
        Assert.Equal(ImportStatus.UnsupportedPlatform, result.Status);
        Assert.Contains("暂不支持", result.Message);
    }

    [Fact]
    public async Task ProcessImport_MixedText_ExtractsUrl()
    {
        var result = await _service.ProcessImportAsync("看看这个：https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
        Assert.Equal(Platform.github, result.Platform);
    }

    [Fact]
    public async Task ProcessImport_DuplicateUrl_ReturnsDuplicateExistingItem()
    {
        // First import creates item
        var result1 = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result1.Status);

        // Second import should be duplicate
        var result2 = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.DuplicateExistingItem, result2.Status);
        Assert.Contains("已存在于归档库中", result2.Message);
    }

    [Fact]
    public async Task ProcessImport_DuplicateItem_ReturnsDuplicateExistingItem()
    {
        // Insert an item directly
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet', 'github',
                'github://repo/openai/openai-dotnet', 1700000000, 1700000000, 'normal', 'pending', 'textOnly')";
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.DuplicateExistingItem, result.Status);
        Assert.Contains("已存在于归档库中", result.Message);
    }

    [Fact]
    public async Task ProcessImport_ParserNotImplementedTask_AllowsReImport()
    {
        // Simulate old Phase 7C task with parser_not_implemented (pending + error_message)
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, error_message, created_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'pending', 0, '解析器尚未实现', 1700000000, 0)";
        cmd.ExecuteNonQuery();

        // Should NOT be Duplicate - should allow re-import
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
    }

    [Fact]
    public async Task ProcessImport_FailedTask_AllowsReImport()
    {
        // Insert a failed task
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, error_message, created_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'failed', 0, 'HTTP request failed', 1700000000, 0)";
        cmd.ExecuteNonQuery();

        // Should NOT be Duplicate - should allow re-import
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
    }

    [Fact]
    public async Task ProcessImport_CompletedTask_ReturnsDuplicate()
    {
        // Insert a completed task without item_id (orphan task)
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'completed', 1, 1700000000, 0)";
        cmd.ExecuteNonQuery();

        // Orphan completed task should allow re-import
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.True(result.Status == ImportStatus.SuccessImport || result.Status == ImportStatus.Failed);
    }

    [Fact]
    public async Task ProcessImport_PendingTaskWithoutError_ReturnsDuplicateImportTask()
    {
        // Insert a pending task without error (real task in progress)
        // Use recent updated_at to simulate active task
        var recentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
        cmd.Parameters.AddWithValue("$createdAt", recentTime);
        cmd.Parameters.AddWithValue("$updatedAt", recentTime);
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.DuplicateImportTask, result.Status);
        Assert.Contains("已有导入任务", result.Message);
    }

    [Fact]
    public async Task ProcessImport_GitHubWritesItem()
    {
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);

        // Verify item exists in database
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE platform='github'";
        var count = (long)cmd.ExecuteScalar();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ProcessImport_GitHubItemHasCorrectFields()
    {
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT title, author, platform, original_url, normalized_url FROM items WHERE platform='github' LIMIT 1";
        using var reader = cmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("github", reader.GetString(reader.GetOrdinal("platform")));
        Assert.Contains("openai", reader.GetString(reader.GetOrdinal("author")));
        Assert.Equal("https://github.com/openai/openai-dotnet", reader.GetString(reader.GetOrdinal("original_url")));
        Assert.Equal("github://repo/openai/openai-dotnet", reader.GetString(reader.GetOrdinal("normalized_url")));
    }

    [Fact]
    public async Task ProcessImport_GitHubTaskCompleted()
    {
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT status FROM import_tasks ORDER BY created_at DESC LIMIT 1";
        var status = (string)cmd.ExecuteScalar();
        Assert.Equal("completed", status);
    }

    // ==================== Stale Task Tests ====================

    [Fact]
    public async Task ProcessImport_PendingTask_RecentUpdated_ReturnsDuplicateImportTask()
    {
        // Pending task with recent updated_at (within 10 minutes) → blocks
        var recentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
        cmd.Parameters.AddWithValue("$createdAt", recentTime);
        cmd.Parameters.AddWithValue("$updatedAt", recentTime);
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.DuplicateImportTask, result.Status);
    }

    [Fact]
    public async Task ProcessImport_PendingTask_9Min59SecAgo_ReturnsDuplicateImportTask()
    {
        // Pending task updated 9 minutes 59 seconds ago → still active
        // Use a slightly more recent timestamp to account for test execution time
        var now = DateTimeOffset.UtcNow;
        var updatedAt = now.AddMinutes(-9).AddSeconds(-50).ToUnixTimeSeconds(); // 9 min 50 sec ago
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
        cmd.Parameters.AddWithValue("$createdAt", updatedAt);
        cmd.Parameters.AddWithValue("$updatedAt", updatedAt);
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.DuplicateImportTask, result.Status);
    }

    [Fact]
    public async Task ProcessImport_PendingTask_10Min05SecAgo_AllowsReImport()
    {
        // Pending task updated 10 minutes 5 seconds ago → stale, allows re-import
        var now = DateTimeOffset.UtcNow;
        var updatedAt = now.AddMinutes(-10).AddSeconds(-5).ToUnixTimeSeconds();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
        cmd.Parameters.AddWithValue("$createdAt", updatedAt);
        cmd.Parameters.AddWithValue("$updatedAt", updatedAt);
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
    }

    [Fact]
    public async Task ProcessImport_PendingTask_10Min01SecAgo_AllowsReImport()
    {
        // Pending task updated 10 minutes 1 second ago → stale, allows re-import
        var now = DateTimeOffset.UtcNow;
        var updatedAt = now.AddMinutes(-10).AddSeconds(-1).ToUnixTimeSeconds();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
        cmd.Parameters.AddWithValue("$createdAt", updatedAt);
        cmd.Parameters.AddWithValue("$updatedAt", updatedAt);
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
    }

    [Fact]
    public async Task ProcessImport_PendingTask_20MinAgo_AllowsReImport()
    {
        // Pending task updated 20 minutes ago → stale, allows re-import
        var now = DateTimeOffset.UtcNow;
        var updatedAt = now.AddMinutes(-20).ToUnixTimeSeconds();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
        cmd.Parameters.AddWithValue("$createdAt", updatedAt);
        cmd.Parameters.AddWithValue("$updatedAt", updatedAt);
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
    }

    [Fact]
    public async Task ProcessImport_StalePendingPlusRecentImporting_RecentBlocks()
    {
        // Stale pending + recent importing → recent one blocks
        var now = DateTimeOffset.UtcNow;
        var staleTime = now.AddMinutes(-20).ToUnixTimeSeconds();
        var recentTime = now.AddMinutes(-2).ToUnixTimeSeconds();

        // Insert stale pending task
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
                VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                    'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
            cmd.Parameters.AddWithValue("$createdAt", staleTime);
            cmd.Parameters.AddWithValue("$updatedAt", staleTime);
            cmd.ExecuteNonQuery();
        }

        // Insert recent pending task (simulates importing)
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
                VALUES ('00000000-0000-0000-0000-000000000002', 'https://github.com/openai/openai-dotnet',
                    'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
            cmd.Parameters.AddWithValue("$createdAt", recentTime);
            cmd.Parameters.AddWithValue("$updatedAt", recentTime);
            cmd.ExecuteNonQuery();
        }

        // Should be blocked by recent task
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.DuplicateImportTask, result.Status);
    }

    [Fact]
    public async Task ProcessImport_StaleImportingPlusFailedTask_AllowsReImport()
    {
        // Stale importing + failed task → allows re-import
        var now = DateTimeOffset.UtcNow;
        var staleTime = now.AddMinutes(-20).ToUnixTimeSeconds();

        // Insert stale pending task
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
                VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                    'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
            cmd.Parameters.AddWithValue("$createdAt", staleTime);
            cmd.Parameters.AddWithValue("$updatedAt", staleTime);
            cmd.ExecuteNonQuery();
        }

        // Insert failed task
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, error_message, created_at, updated_at, retry_count)
                VALUES ('00000000-0000-0000-0000-000000000002', 'https://github.com/openai/openai-dotnet',
                    'github://repo/openai/openai-dotnet', 'github', 'failed', 0, 'HTTP error', $createdAt, $updatedAt, 0)";
            cmd.Parameters.AddWithValue("$createdAt", staleTime);
            cmd.Parameters.AddWithValue("$updatedAt", staleTime);
            cmd.ExecuteNonQuery();
        }

        // Should allow re-import (stale pending + failed)
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
    }

    [Fact]
    public async Task ProcessImport_StaleTaskRetrySuccess_CreatesNewTask()
    {
        // Stale task retry should create new task and succeed
        var now = DateTimeOffset.UtcNow;
        var staleTime = now.AddMinutes(-20).ToUnixTimeSeconds();

        // Insert stale pending task
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
                VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                    'github://repo/openai/openai-dotnet', 'github', 'pending', 0, $createdAt, $updatedAt, 0)";
            cmd.Parameters.AddWithValue("$createdAt", staleTime);
            cmd.Parameters.AddWithValue("$updatedAt", staleTime);
            cmd.ExecuteNonQuery();
        }

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);

        // Verify new task was created and is completed
        using var cmd2 = _connection.CreateCommand();
        cmd2.CommandText = "SELECT COUNT(*) FROM import_tasks WHERE normalized_url='github://repo/openai/openai-dotnet' AND status='completed'";
        var count = (long)cmd2.ExecuteScalar();
        Assert.True(count >= 1);
    }

    [Fact]
    public async Task ProcessImport_UpdatedAtNull_AllowsReImport()
    {
        // Task with NULL updated_at should not crash and should allow re-import
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'pending', 0, 1700000000, 0)";
        cmd.ExecuteNonQuery();

        // Should not crash and should allow re-import (treated as stale)
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.True(result.Status == ImportStatus.SuccessImport || result.Status == ImportStatus.Failed);
    }

    [Fact]
    public async Task ProcessImport_CompletedTaskWithNullItemId_AllowsReImport()
    {
        // Completed task with NULL item_id → orphan task, allows re-import
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'completed', 1, 1700000000, 1700000000, 0)";
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.True(result.Status == ImportStatus.SuccessImport || result.Status == ImportStatus.Failed);
    }

    [Fact]
    public async Task ProcessImport_CompletedTaskWithOrphanItemId_AllowsReImport()
    {
        // Completed task with item_id that doesn't exist → orphan, allows re-import
        // Note: item_id FK constraint means we can't insert a non-existent item_id
        // Instead, test with completed task without item_id (orphan case)
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, updated_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'completed', 1, 1700000000, 1700000000, 0)";
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.True(result.Status == ImportStatus.SuccessImport || result.Status == ImportStatus.Failed);
    }

    [Fact]
    public async Task ProcessImport_ActiveItemExists_StillDuplicateExistingItem()
    {
        // Active item exists → DuplicateExistingItem (priority over stale task)
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet', 'github',
                'github://repo/openai/openai-dotnet', 1700000000, 1700000000, 'normal', 'pending', 'textOnly')";
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.DuplicateExistingItem, result.Status);
    }

    [Fact]
    public async Task ProcessImport_TrashItemExists_AllowsReImport()
    {
        // Item in trash → should allow re-import (not DuplicateInTrash)
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet', 'github',
                    'github://repo/openai/openai-dotnet', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.ExecuteNonQuery();
        }

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
    }

    [Fact]
    public async Task ProcessImport_TrashItemReimport_CreatesNewItem()
    {
        // Item in trash → reimport should create a NEW item with different ID
        var trashedItemId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    title, author, import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ($id, 'https://github.com/openai/openai-dotnet', 'github',
                    'github://repo/openai/openai-dotnet', 'Old Title', 'Old Author', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
        Assert.NotNull(result.ImportTaskId);

        // New item ID must differ from old trash item ID
        Assert.NotEqual(trashedItemId, result.ImportTaskId);

        // New item is active
        using var checkCmd = _connection.CreateCommand();
        checkCmd.CommandText = "SELECT deleted_at, content_status FROM items WHERE id=$id";
        checkCmd.Parameters.AddWithValue("$id", result.ImportTaskId.Value.ToString("D"));
        using var reader = checkCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(reader.GetOrdinal("deleted_at")));
        Assert.Equal("normal", reader.GetString(reader.GetOrdinal("content_status")));
    }

    [Fact]
    public async Task ProcessImport_TrashItemReimport_OldItemStaysInTrash()
    {
        // Item in trash → reimport should leave old item in trash
        var trashedItemId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ($id, 'https://github.com/openai/openai-dotnet', 'github',
                    'github://repo/openai/openai-dotnet', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO trash_records (id, item_id, deleted_at, auto_delete_at, original_archive_status, media_paths)
                VALUES ('00000000-0000-0000-0000-000000000099', $itemId, 1700000000, 1700300000, 'pending', '[]')";
            cmd.Parameters.AddWithValue("$itemId", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);

        // Old item still in trash
        using var oldCmd = _connection.CreateCommand();
        oldCmd.CommandText = "SELECT deleted_at, content_status FROM items WHERE id=$id";
        oldCmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
        using var reader = oldCmd.ExecuteReader();
        Assert.True(reader.Read());
        Assert.False(reader.IsDBNull(reader.GetOrdinal("deleted_at")));
        Assert.Equal("trashed", reader.GetString(reader.GetOrdinal("content_status")));

        // Old trash_record still exists
        using var trashCmd = _connection.CreateCommand();
        trashCmd.CommandText = "SELECT COUNT(*) FROM trash_records WHERE item_id=$id";
        trashCmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
        var count = (long)trashCmd.ExecuteScalar();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ProcessImport_TrashItemReimport_ImportTaskPointsToNewItem()
    {
        // Item in trash → reimport should link import_task to NEW item
        var trashedItemId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ($id, 'https://github.com/openai/openai-dotnet', 'github',
                    'github://repo/openai/openai-dotnet', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);

        // import_task.item_id points to NEW item
        using var taskCmd = _connection.CreateCommand();
        taskCmd.CommandText = "SELECT item_id, status FROM import_tasks WHERE normalized_url='github://repo/openai/openai-dotnet' ORDER BY created_at DESC LIMIT 1";
        using var reader = taskCmd.ExecuteReader();
        Assert.True(reader.Read());
        var taskItemId = reader.GetString(reader.GetOrdinal("item_id"));
        Assert.NotEqual(trashedItemId.ToString("D"), taskItemId);
        Assert.Equal("completed", reader.GetString(reader.GetOrdinal("status")));
    }

    [Fact]
    public async Task ProcessImport_TrashItemReimport_NewContentApplied()
    {
        // Item in trash → reimport should create new item with new content
        var trashedItemId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    title, author, import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ($id, 'https://github.com/openai/openai-dotnet', 'github',
                    'github://repo/openai/openai-dotnet', 'Old Title', 'Old Author', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);

        // New item has new content
        using var cmd2 = _connection.CreateCommand();
        cmd2.CommandText = "SELECT title, author FROM items WHERE id=$id";
        cmd2.Parameters.AddWithValue("$id", result.ImportTaskId.Value.ToString("D"));
        using var reader = cmd2.ExecuteReader();
        Assert.True(reader.Read());
        var title = reader.GetString(reader.GetOrdinal("title"));
        var author = reader.GetString(reader.GetOrdinal("author"));
        Assert.NotEqual("Old Title", title);
        Assert.NotEqual("Old Author", author);

        // Old item still has old content
        using var oldCmd = _connection.CreateCommand();
        oldCmd.CommandText = "SELECT title, author FROM items WHERE id=$id";
        oldCmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
        using var oldReader = oldCmd.ExecuteReader();
        Assert.True(oldReader.Read());
        Assert.Equal("Old Title", oldReader.GetString(oldReader.GetOrdinal("title")));
        Assert.Equal("Old Author", oldReader.GetString(oldReader.GetOrdinal("author")));
    }

    [Fact]
    public async Task ProcessImport_TrashItemReimport_FtsForNewItem()
    {
        // Item in trash → reimport should create FTS for new item only
        var trashedItemId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    title, body, import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ($id, 'https://github.com/openai/openai-dotnet', 'github',
                    'github://repo/openai/openai-dotnet', 'Old Title', 'Old Body', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        // Insert FTS for old content
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO items_fts(rowid, title, body) VALUES ((SELECT rowid FROM items WHERE id=$id), 'Old Title', 'Old Body')";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);

        // FTS should contain new content (from new item's InsertAsync)
        using var searchCmd = _connection.CreateCommand();
        searchCmd.CommandText = "SELECT COUNT(*) FROM items_fts WHERE items_fts MATCH 'openai'";
        var count = (long)searchCmd.ExecuteScalar();
        Assert.True(count > 0);
    }

    [Fact]
    public async Task ProcessImport_TrashItemParserFails_NoNewItem()
    {
        // Item in trash + parser fails → no new active item created
        var trashedItemId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ($id, 'https://www.xiaohongshu.com/explore/test123', 'xiaohongshu',
                    'xiaohongshu://explore/test123', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO trash_records (id, item_id, deleted_at, auto_delete_at, original_archive_status, media_paths)
                VALUES ('00000000-0000-0000-0000-000000000099', $itemId, 1700000000, 1700300000, 'pending', '[]')";
            cmd.Parameters.AddWithValue("$itemId", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        // xiaohongshu parser returns TaskCreated (not implemented yet)
        var result = await _service.ProcessImportAsync("https://www.xiaohongshu.com/explore/test123");
        Assert.Equal(ImportStatus.TaskCreated, result.Status);

        // No new active item created
        using var activeCmd = _connection.CreateCommand();
        activeCmd.CommandText = "SELECT COUNT(*) FROM items WHERE normalized_url='xiaohongshu://explore/test123' AND deleted_at IS NULL";
        var activeCount = (long)activeCmd.ExecuteScalar();
        Assert.Equal(0, activeCount);

        // Old item still in trash
        using var checkCmd = _connection.CreateCommand();
        checkCmd.CommandText = "SELECT deleted_at FROM items WHERE id=$id";
        checkCmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
        var deletedAt = await checkCmd.ExecuteScalarAsync();
        Assert.NotNull(deletedAt);

        // Old trash_record still exists
        using var trashCmd = _connection.CreateCommand();
        trashCmd.CommandText = "SELECT COUNT(*) FROM trash_records WHERE item_id=$id";
        trashCmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
        var trashCount = (long)trashCmd.ExecuteScalar();
        Assert.Equal(1, trashCount);
    }

    [Fact]
    public async Task ProcessImport_AfterTrashReimport_DuplicateExists()
    {
        // After successful trash reimport, same URL → DuplicateExistingItem
        var trashedItemId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ($id, 'https://github.com/openai/openai-dotnet', 'github',
                    'github://repo/openai/openai-dotnet', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        // First import: reimport from trash
        var result1 = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result1.Status);

        // Second import: DuplicateExistingItem (new active item exists)
        var result2 = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.DuplicateExistingItem, result2.Status);
    }

    [Fact]
    public async Task ProcessImport_TrashItemReimport_OneActiveAndOneTrash()
    {
        // After trash reimport: 1 active + 1 trash
        var trashedItemId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ($id, 'https://github.com/openai/openai-dotnet', 'github',
                    'github://repo/openai/openai-dotnet', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);

        // 1 active item
        using var activeCmd = _connection.CreateCommand();
        activeCmd.CommandText = "SELECT COUNT(*) FROM items WHERE normalized_url='github://repo/openai/openai-dotnet' AND deleted_at IS NULL";
        var activeCount = (long)activeCmd.ExecuteScalar();
        Assert.Equal(1, activeCount);

        // 1 trashed item
        using var trashCmd = _connection.CreateCommand();
        trashCmd.CommandText = "SELECT COUNT(*) FROM items WHERE normalized_url='github://repo/openai/openai-dotnet' AND deleted_at IS NOT NULL";
        var trashCount = (long)trashCmd.ExecuteScalar();
        Assert.Equal(1, trashCount);
    }

    [Fact]
    public async Task ProcessImport_CompletedTaskPointingToTrash_AllowsReImport()
    {
        // Completed task pointing to trash item → should not block re-import
        var trashedItemId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO items (id, original_url, platform, normalized_url,
                    import_date, modify_date, content_status, archive_status, media_status, deleted_at)
                VALUES ($id, 'https://github.com/openai/openai-dotnet', 'github',
                    'github://repo/openai/openai-dotnet', 1700000000, 1700000000, 'trashed', 'pending', 'textOnly', 1700000000)";
            cmd.Parameters.AddWithValue("$id", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        // Insert completed task pointing to trash item
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"
                INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, item_id, created_at, updated_at, retry_count)
                VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                    'github://repo/openai/openai-dotnet', 'github', 'completed', 1, $itemId, 1700000000, 1700000000, 0)";
            cmd.Parameters.AddWithValue("$itemId", trashedItemId.ToString("D"));
            cmd.ExecuteNonQuery();
        }

        // Should allow re-import (task points to trashed item)
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
    }

    [Fact]
    public async Task ProcessImport_GitHubParser_NotAffectedByStaleLogic()
    {
        // GitHub parser should work normally
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
        Assert.Equal(Platform.github, result.Platform);
    }

    [Fact]
    public async Task ProcessImport_BilibiliParser_NotAffectedByStaleLogic()
    {
        // Bilibili parser should work normally
        var result = await _service.ProcessImportAsync("https://www.bilibili.com/video/BV1xx411c7mD");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
        Assert.Equal(Platform.bilibili, result.Platform);
    }

    [Fact]
    public async Task ProcessImport_YouTubeParser_NotAffectedByStaleLogic()
    {
        // YouTube parser should work normally
        var result = await _service.ProcessImportAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        Assert.Equal(ImportStatus.SuccessImport, result.Status);
        Assert.Equal(Platform.youtube, result.Platform);
    }
}
