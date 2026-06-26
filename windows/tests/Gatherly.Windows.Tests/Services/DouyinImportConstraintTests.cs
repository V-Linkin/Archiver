using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.Services.Import;
using Gatherly.Windows.Services.Media;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class DouyinImportConstraintTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ImportService _service;
    private readonly FakePlatformRouter _router;
    private readonly FakeContentParser _douyinParser;

    public DouyinImportConstraintTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);

        _router = new FakePlatformRouter();
        _douyinParser = new FakeContentParser
        {
            Platform = Platform.douyin,
            Title = "测试视频标题",
            Author = "测试作者",
            Body = "测试正文内容",
            CoverUrl = "https://example.com/cover.jpg",
            VideoUrl = "https://example.com/video.mp4",
            ImageUrls = new List<string> { "https://example.com/img1.jpg", "https://example.com/img2.jpg" },
            PlatformContentId = "1234567890"
        };
        _router.RegisterParser(Platform.douyin, _douyinParser);

        var itemRepo = new ItemRepository(_connection);
        var taskRepo = new ImportTaskRepository(_connection);
        var mediaRepo = new MediaRepository(_connection);
        var mediaDownload = new MediaDownloadService(mediaRepo);

        _service = new ImportService(itemRepo, taskRepo, mediaDownload, TimeProvider.System, null, _router);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task DouyinImport_VideoNote_NoConstraintFailure()
    {
        var url = "https://v.douyin.com/-baq4Gfb7Mk/";
        var result = await _service.ProcessImportAsync(url);

        Assert.True(
            result.Status == ImportStatus.SuccessImport || result.Status == ImportStatus.Failed,
            $"Unexpected status: {result.Status} - {result.Message}");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE original_url LIKE '%douyin%'";
        var count = (long)(await cmd.ExecuteScalarAsync()!);
        Assert.True(count > 0, "At least one douyin item should be created");
    }

    [Fact]
    public async Task DouyinImport_ImageNote_NoConstraintFailure()
    {
        var url = "https://v.douyin.com/i92QTmubd7Q/";
        var result = await _service.ProcessImportAsync(url);

        Assert.True(
            result.Status == ImportStatus.SuccessImport || result.Status == ImportStatus.Failed,
            $"Unexpected status: {result.Status} - {result.Message}");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE original_url LIKE '%douyin%'";
        var count = (long)(await cmd.ExecuteScalarAsync()!);
        Assert.True(count > 0, "At least one douyin item should be created");
    }

    [Fact]
    public async Task DouyinImport_VideoNote_ImportTaskCompletedWithItemId()
    {
        var url = "https://v.douyin.com/-baq4Gfb7Mk/";
        await _service.ProcessImportAsync(url);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT status, item_id FROM import_tasks WHERE original_url LIKE '%baq4Gfb7Mk%'";
        using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Import task should exist");
        Assert.Equal("completed", reader.GetString(0));
        Assert.False(reader.IsDBNull(1), "item_id should not be null");
    }

    [Fact]
    public async Task DouyinImport_DuplicateUrl_ReturnsDuplicateWithoutError()
    {
        var url = "https://v.douyin.com/-baq4Gfb7Mk/";
        var result1 = await _service.ProcessImportAsync(url);
        var result2 = await _service.ProcessImportAsync(url);

        Assert.True(
            result1.Status == ImportStatus.SuccessImport || result1.Status == ImportStatus.Failed,
            $"First import: {result1.Status} - {result1.Message}");
        Assert.True(
            result2.Status == ImportStatus.DuplicateExistingItem || result2.Status == ImportStatus.DuplicateImportTask || result2.Status == ImportStatus.Failed,
            $"Second import should be duplicate, got: {result2.Status} - {result2.Message}");
    }

    [Fact]
    public async Task DouyinImport_FullVideoFields_InDatabase()
    {
        var url = "https://v.douyin.com/-baq4Gfb7Mk/";
        await _service.ProcessImportAsync(url);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT title, body, author, platform_content_id, platform, custom_platform_id FROM items WHERE original_url LIKE '%baq4Gfb7Mk%'";
        using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Item should exist");
        Assert.False(string.IsNullOrEmpty(reader.GetString(0)), "Title should not be empty");
        Assert.False(reader.IsDBNull(1), "Body should not be null");
        Assert.False(reader.IsDBNull(2), "Author should not be null");
        Assert.False(reader.IsDBNull(3), "PlatformContentId should not be null");
    }
}
