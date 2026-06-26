using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.Services.Import;
using Gatherly.Windows.Services.Media;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;
using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace Gatherly.Windows.Tests.Services;

public class DoubanImportChainTests
{
    private readonly ITestOutputHelper _output;

    public DoubanImportChainTests(ITestOutputHelper output) { _output = output; }

    [Fact]
    public async Task DoubanUrl_ShouldRouteToDoubanParser()
    {
        var url = "https://movie.douban.com/subject/1301168/";
        var platform = UrlNormalizer.RecognizePlatform(url);
        _output.WriteLine($"Platform: {platform}");

        Assert.Equal(Platform.douban, platform);

        var router = new PlatformRouter();
        var parser = router.GetParser(platform.Value, url);
        _output.WriteLine($"Parser type: {parser.GetType().Name}");

        Assert.IsType<DoubanParser>(parser);
        Assert.False(parser is NotImplementedParser);
    }

    [Fact]
    public async Task DoubanImport_ShouldNotReturnUnsupported()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        MigrationRunner.RunAll(connection);

        var itemRepo = new ItemRepository(connection);
        var taskRepo = new ImportTaskRepository(connection);
        var mediaRepo = new MediaRepository(connection);
        var mediaDownload = new MediaDownloadService(mediaRepo);
        var service = new ImportService(itemRepo, taskRepo, mediaDownload, TimeProvider.System, null);

        var url = "https://movie.douban.com/subject/1301168/";
        var result = await service.ProcessImportAsync(url);

        _output.WriteLine($"Status: {result.Status}");
        _output.WriteLine($"Message: {result.Message}");

        Assert.NotEqual(ImportStatus.TaskCreated, result.Status);
        Assert.DoesNotContain("后续阶段支持", result.Message ?? "");
    }

    [Fact]
    public async Task AllExistingParsers_StillRouteCorrectly()
    {
        var router = new PlatformRouter();
        var cases = new (Platform platform, Type expected, string url)[]
        {
            (Platform.douyin, typeof(DouyinParser), "https://v.douyin.com/abc"),
            (Platform.xiaohongshu, typeof(XiaohongshuParser), "https://www.xiaohongshu.com/explore/abc"),
            (Platform.coolapk, typeof(CoolapkParser), "https://www.coolapk.com/feed/123"),
            (Platform.weibo, typeof(WeiboParser), "https://weibo.com/status/123"),
            (Platform.zhihu, typeof(ZhihuParser), "https://www.zhihu.com/question/123/answer/456"),
            (Platform.douban, typeof(DoubanParser), "https://movie.douban.com/subject/123"),
        };

        foreach (var (platform, expected, url) in cases)
        {
            var parser = router.GetParser(platform, url);
            Assert.IsType(expected, parser);
            _output.WriteLine($"{platform} -> {parser.GetType().Name} ✓");
        }
    }
}
