using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.Services.Import;
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
        _service = new ImportService(new ItemRepository(_connection), new ImportTaskRepository(_connection));
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
    public async Task ProcessImport_GitHubUrl_ReturnsTaskCreated()
    {
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.TaskCreated, result.Status);
        Assert.Equal(Platform.github, result.Platform);
        Assert.Contains("GitHub", result.Message);
        Assert.NotNull(result.ImportTaskId);
    }

    [Fact]
    public async Task ProcessImport_BilibiliUrl_ReturnsTaskCreated()
    {
        var result = await _service.ProcessImportAsync("https://www.bilibili.com/video/BV1xx411c7mD");
        Assert.Equal(ImportStatus.TaskCreated, result.Status);
        Assert.Equal(Platform.bilibili, result.Platform);
        Assert.Contains("B站", result.Message);
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
        Assert.Equal(ImportStatus.TaskCreated, result.Status);
        Assert.Equal(Platform.github, result.Platform);
    }

    [Fact]
    public async Task ProcessImport_DuplicateUrl_ReturnsDuplicate()
    {
        // First import creates task
        var result1 = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.TaskCreated, result1.Status);

        // Second import should be duplicate
        var result2 = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.Duplicate, result2.Status);
        Assert.Contains("已有导入任务", result2.Message);
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
    public async Task ProcessImport_CreatesImportTask()
    {
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.TaskCreated, result.Status);
        Assert.NotNull(result.ImportTaskId);

        // Verify task exists in database
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM import_tasks WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", result.ImportTaskId!.Value.ToString("D"));
        var count = (long)cmd.ExecuteScalar();
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task ProcessImport_TaskHasCorrectStatus()
    {
        var result = await _service.ProcessImportAsync("https://github.com/openai/openai-dotnet");
        Assert.NotNull(result.ImportTaskId);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT status FROM import_tasks WHERE id COLLATE NOCASE=$id";
        cmd.Parameters.AddWithValue("$id", result.ImportTaskId!.Value.ToString("D"));
        var status = (string)cmd.ExecuteScalar();
        Assert.Equal("pending", status);
    }
}
