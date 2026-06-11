using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class ImportServiceTests
{
    private readonly ImportService _service = new();

    [Fact]
    public void ProcessImport_EmptyInput_ReturnsEmptyInput()
    {
        var result = _service.ProcessImport(null);
        Assert.Equal(ImportStatus.EmptyInput, result.Status);
        Assert.Equal("请输入或粘贴链接", result.Message);
    }

    [Fact]
    public void ProcessImport_Whitespace_ReturnsEmptyInput()
    {
        var result = _service.ProcessImport("   ");
        Assert.Equal(ImportStatus.EmptyInput, result.Status);
    }

    [Fact]
    public void ProcessImport_PlainText_ReturnsInvalidUrl()
    {
        var result = _service.ProcessImport("这是一段普通文字");
        Assert.Equal(ImportStatus.InvalidUrl, result.Status);
        Assert.Equal("输入的内容不是有效的 URL", result.Message);
    }

    [Fact]
    public void ProcessImport_GitHubUrl_ReturnsPlatformRecognized()
    {
        var result = _service.ProcessImport("https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.UnsupportedPendingParser, result.Status);
        Assert.Equal(Platform.github, result.DetectedPlatform);
        Assert.Contains("GitHub", result.Message);
    }

    [Fact]
    public void ProcessImport_BilibiliUrl_ReturnsPlatformRecognized()
    {
        var result = _service.ProcessImport("https://www.bilibili.com/video/BV1xx411c7mD");
        Assert.Equal(ImportStatus.UnsupportedPendingParser, result.Status);
        Assert.Equal(Platform.bilibili, result.DetectedPlatform);
        Assert.Contains("B站", result.Message);
    }

    [Fact]
    public void ProcessImport_YouTubeUrl_ReturnsPlatformRecognized()
    {
        var result = _service.ProcessImport("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        Assert.Equal(ImportStatus.UnsupportedPendingParser, result.Status);
        Assert.Equal(Platform.youtube, result.DetectedPlatform);
        Assert.Contains("YouTube", result.Message);
    }

    [Fact]
    public void ProcessImport_XiaohongshuUrl_ReturnsPlatformRecognized()
    {
        var result = _service.ProcessImport("https://www.xiaohongshu.com/explore/65a1b2c3");
        Assert.Equal(ImportStatus.UnsupportedPendingParser, result.Status);
        Assert.Equal(Platform.xiaohongshu, result.DetectedPlatform);
        Assert.Contains("小红书", result.Message);
    }

    [Fact]
    public void ProcessImport_UnknownUrl_ReturnsUnsupportedPlatform()
    {
        var result = _service.ProcessImport("https://www.example.com/article/123");
        Assert.Equal(ImportStatus.UnsupportedPlatform, result.Status);
        Assert.Contains("暂不支持", result.Message);
    }

    [Fact]
    public void ProcessImport_MixedText_ExtractsUrl()
    {
        var result = _service.ProcessImport("看看这个：https://github.com/openai/openai-dotnet");
        Assert.Equal(ImportStatus.UnsupportedPendingParser, result.Status);
        Assert.Equal(Platform.github, result.DetectedPlatform);
    }

    [Fact]
    public void ProcessImport_YoutuBe_ReturnsYouTube()
    {
        var result = _service.ProcessImport("https://youtu.be/dQw4w9WgXcQ");
        Assert.Equal(Platform.youtube, result.DetectedPlatform);
    }

    [Fact]
    public void ProcessImport_Twitter_ReturnsX()
    {
        var result = _service.ProcessImport("https://twitter.com/user/status/1234567890");
        Assert.Equal(Platform.x, result.DetectedPlatform);
    }

    [Fact]
    public void ProcessImport_Douyin_ReturnsDouyin()
    {
        var result = _service.ProcessImport("https://www.douyin.com/video/7301234567890");
        Assert.Equal(Platform.douyin, result.DetectedPlatform);
    }

    [Fact]
    public void ProcessImport_Weibo_ReturnsWeibo()
    {
        var result = _service.ProcessImport("https://weibo.com/status/4892046789012");
        Assert.Equal(Platform.weibo, result.DetectedPlatform);
    }

    [Fact]
    public void ProcessImport_Zhihu_ReturnsZhihu()
    {
        var result = _service.ProcessImport("https://www.zhihu.com/question/12345678/answer/87654321");
        Assert.Equal(Platform.zhihu, result.DetectedPlatform);
    }

    [Fact]
    public void ProcessImport_Douban_ReturnsDouban()
    {
        var result = _service.ProcessImport("https://movie.douban.com/subject/35517853/");
        Assert.Equal(Platform.douban, result.DetectedPlatform);
    }

    [Fact]
    public void ProcessImport_Coolapk_ReturnsCoolapk()
    {
        var result = _service.ProcessImport("https://www.coolapk.com/feed/12345678");
        Assert.Equal(Platform.coolapk, result.DetectedPlatform);
    }
}
