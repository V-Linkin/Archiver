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
    public async Task ProcessImport_YouTubeUrl_ReturnsTaskCreated()
    {
        var result = await _service.ProcessImportAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        Assert.Equal(ImportStatus.TaskCreated, result.Status);
        Assert.Equal(Platform.youtube, result.Platform);
        Assert.Contains("YouTube", result.Message);
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
    public async Task ProcessImport_DuplicateUrl_ReturnsDuplicate()
    {
        // First import creates item
        var result1 = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.SuccessImport, result1.Status);

        // Second import should be duplicate
        var result2 = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.Duplicate, result2.Status);
        Assert.Contains("已存在于归档库中", result2.Message);
    }

    [Fact]
    public async Task ProcessImport_DuplicateItem_ReturnsDuplicate()
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
        Assert.Equal(ImportStatus.Duplicate, result.Status);
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
        // Insert a completed task
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'completed', 1, 1700000000, 0)";
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.Duplicate, result.Status);
        Assert.Contains("已有导入任务", result.Message);
    }

    [Fact]
    public async Task ProcessImport_PendingTaskWithoutError_ReturnsDuplicate()
    {
        // Insert a pending task without error (real task in progress)
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO import_tasks (id, original_url, normalized_url, platform, status, progress, created_at, retry_count)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://github.com/openai/openai-dotnet',
                'github://repo/openai/openai-dotnet', 'github', 'pending', 0, 1700000000, 0)";
        cmd.ExecuteNonQuery();

        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.Duplicate, result.Status);
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
}
