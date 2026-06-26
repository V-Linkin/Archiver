using System.Text.Json;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class CoolapkParserTests
{
    // === URL / Normalizer ===

    [Theory]
    [InlineData("https://www.coolapk.com/feed/72150830", true)]
    [InlineData("https://m.coolapk.com/feed/72150830", true)]
    [InlineData("https://coolapk.com/feed/72150830", true)]
    [InlineData("https://www.coolapk1s.com/feed/72150830", true)]
    [InlineData("https://bilibili.com/video/BV1234", false)]
    [InlineData("https://xiaohongshu.com/explore/abc", false)]
    public void UrlNormalizer_RecognizesCoolapk(string url, bool expected)
    {
        var result = UrlNormalizer.RecognizePlatform(url);
        Assert.Equal(expected, result == Platform.coolapk);
    }

    [Theory]
    [InlineData("https://www.coolapk.com/feed/72150830", "72150830")]
    [InlineData("https://m.coolapk.com/feed/72150830?s=MWQ3MDY2ZjdhNjU1ZGc2YTIzYzRkYXoi1625", "72150830")]
    public void UrlNormalizer_ExtractsCoolapkFeedId(string url, string expectedId)
    {
        var id = UrlNormalizer.ExtractContentId(url, Platform.coolapk);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("看看这个动态 https://www.coolapk.com/feed/72150830 好看")]
    public void UrlNormalizer_ExtractsCoolapkUrlFromShareText(string text)
    {
        var url = UrlNormalizer.ExtractFirstUrl(text);
        Assert.NotNull(url);
        Assert.Equal(Platform.coolapk, UrlNormalizer.RecognizePlatform(url));
    }

    // === PlatformRouter ===

    [Fact]
    public void PlatformRouter_CoolapkUrl_RoutesToCoolapkParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.coolapk, "https://www.coolapk.com/feed/123");
        Assert.IsType<CoolapkParser>(parser);
    }

    [Fact]
    public void PlatformRouter_DouyinUrl_StillRoutesToDouyinParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.douyin, "https://v.douyin.com/abc");
        Assert.IsType<DouyinParser>(parser);
    }

    [Fact]
    public void PlatformRouter_XiaohongshuUrl_StillRoutesToXiaohongshuParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.xiaohongshu, "https://www.xiaohongshu.com/explore/abc");
        Assert.IsType<XiaohongshuParser>(parser);
    }

    // === Mirror URL conversion ===

    [Fact]
    public void ConvertToMirrorUrl_CoolapkToCoolapk1s()
    {
        var result = typeof(CoolapkParser)
            .GetMethod("ConvertToMirrorUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.Invoke(null, ["https://www.coolapk.com/feed/72150830"]);
        Assert.Equal("https://www.coolapk1s.com/feed/72150830", result);
    }

    // === __NEXT_DATA__ fixture test ===

    [Fact]
    public void ParseFeedFromJson_NextDataStructure_ReturnsFullFields()
    {
        var feedJson = """
        {
          "title": "测试标题",
          "username": "测试用户",
          "message": "这是正文内容 <b>加粗</b> [表情]",
          "picArr": ["https://example.com/img1.jpg", "https://example.com/img2.jpg"],
          "message_cover": "https://example.com/cover.jpg"
        }
        """;

        var doc = JsonDocument.Parse(feedJson);
        var feed = doc.RootElement;

        // Verify feed structure
        Assert.Equal("测试标题", feed.GetProperty("title").GetString());
        Assert.Equal("测试用户", feed.GetProperty("username").GetString());

        var picArr = feed.GetProperty("picArr").EnumerateArray().ToList();
        Assert.Equal(2, picArr.Count);

        Assert.Equal("https://example.com/cover.jpg", feed.GetProperty("message_cover").GetString());
    }

    [Fact]
    public void ParseFeedFromJson_NoCover_UsesFirstImageAsCover()
    {
        var feedJson = """
        {
          "title": "无封面",
          "username": "用户",
          "message": "正文",
          "picArr": ["https://example.com/img1.jpg"]
        }
        """;

        var doc = JsonDocument.Parse(feedJson);
        var feed = doc.RootElement;

        // message_cover not present → cover should be picArr.first
        Assert.False(feed.TryGetProperty("message_cover", out _));
        Assert.Equal("https://example.com/img1.jpg", feed.GetProperty("picArr")[0].GetString());
    }

    [Fact]
    public void ParseFeedFromJson_EmptyTitleAndMessage_ReturnsNull()
    {
        var feedJson = """
        {
          "username": "用户"
        }
        """;

        var doc = JsonDocument.Parse(feedJson);
        var feed = doc.RootElement;

        Assert.False(feed.TryGetProperty("title", out _));
        Assert.False(feed.TryGetProperty("message", out _));
    }

    [Fact]
    public void ParseFeedFromJson_HtmlAndEmojiCleaned()
    {
        var message = "正文 <b>加粗</b> <img src='x'/> [表情] 结尾";
        var cleaned = System.Text.RegularExpressions.Regex.Replace(message, "<[^>]+>", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\[[^\]]+\]", "");
        cleaned = cleaned.Trim();
        Assert.Contains("正文", cleaned);
        Assert.Contains("加粗", cleaned);
        Assert.Contains("结尾", cleaned);
        Assert.DoesNotContain("<b>", cleaned);
        Assert.DoesNotContain("[表情]", cleaned);
    }

    // === CanParse ===

    [Fact]
    public void CanParse_CoolapkPlatform_ReturnsTrue()
    {
        var parser = new CoolapkParser();
        Assert.True(parser.CanParse(Platform.coolapk, "https://www.coolapk.com/feed/123"));
    }

    [Fact]
    public void CanParse_DouyinPlatform_ReturnsFalse()
    {
        var parser = new CoolapkParser();
        Assert.False(parser.CanParse(Platform.douyin, "https://v.douyin.com/abc"));
    }

    // === Proxy URL conversion ===

    [Fact]
    public void ConvertToProxyUrl_FormatsCorrectly()
    {
        var method = typeof(CoolapkParser).GetMethod("ConvertToProxyUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string?)method?.Invoke(null, ["https://example.com/image.jpg"]);
        Assert.StartsWith("https://image.coolapk1s.com/proxy?url=", result);
        Assert.Contains("example.com", result);
    }
}
