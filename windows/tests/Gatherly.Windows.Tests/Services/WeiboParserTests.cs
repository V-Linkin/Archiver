using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class WeiboParserTests
{
    // === URL / Normalizer ===

    [Theory]
    [InlineData("https://weibo.com/status/5155949834992299", true)]
    [InlineData("https://m.weibo.cn/detail/5155949834992299", true)]
    [InlineData("https://m.weibo.cn/status/5155949834992299", true)]
    [InlineData("https://www.weibo.com/status/5155949834992299", true)]
    [InlineData("https://weibo.com/1234567890/abc123", true)]
    [InlineData("https://bilibili.com/video/BV1234", false)]
    [InlineData("https://xiaohongshu.com/explore/abc", false)]
    public void UrlNormalizer_RecognizesWeibo(string url, bool expected)
    {
        var result = UrlNormalizer.RecognizePlatform(url);
        Assert.Equal(expected, result == Platform.weibo);
    }

    [Theory]
    [InlineData("https://weibo.com/status/5155949834992299", "5155949834992299")]
    [InlineData("https://m.weibo.cn/detail/5155949834992299", "5155949834992299")]
    [InlineData("https://m.weibo.cn/status/5155949834992299", "5155949834992299")]
    public void UrlNormalizer_ExtractsWeiboStatusId(string url, string expectedId)
    {
        var id = UrlNormalizer.ExtractContentId(url, Platform.weibo);
        Assert.Equal(expectedId, id);
    }

    [Fact]
    public void UrlNormalizer_ExtractWeiboId_Works()
    {
        var id = UrlNormalizer.ExtractWeiboId("https://weibo.com/status/5155949834992299");
        Assert.Equal("5155949834992299", id);
    }

    [Fact]
    public void UrlNormalizer_ExtractWeiboId_MobileDetail()
    {
        var id = UrlNormalizer.ExtractWeiboId("https://m.weibo.cn/detail/5155949834992299");
        Assert.Equal("5155949834992299", id);
    }

    // === PlatformRouter ===

    [Fact]
    public void PlatformRouter_WeiboUrl_RoutesToWeiboParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.weibo, "https://weibo.com/status/123");
        Assert.IsType<WeiboParser>(parser);
    }

    [Fact]
    public void PlatformRouter_OtherPlatforms_NotAffected()
    {
        var router = new PlatformRouter();
        Assert.IsType<DouyinParser>(router.GetParser(Platform.douyin, "https://v.douyin.com/abc"));
        Assert.IsType<XiaohongshuParser>(router.GetParser(Platform.xiaohongshu, "https://www.xiaohongshu.com/explore/abc"));
        Assert.IsType<CoolapkParser>(router.GetParser(Platform.coolapk, "https://www.coolapk.com/feed/123"));
    }

    // === stripHTML ===

    [Fact]
    public void StripHtml_RemovesTagsAndEntities()
    {
        var input = "Hello <b>world</b><br/>New line &amp; &nbsp; spaces";
        var result = WeiboParser.StripHtml(input);
        Assert.Contains("Hello", result);
        Assert.Contains("world", result);
        Assert.DoesNotContain("<b>", result);
        Assert.Contains("&", result);
        Assert.Contains(" ", result);
    }

    [Fact]
    public void StripHtml_DecodesHTMLEntities()
    {
        var input = "&amp; &lt; &gt; &nbsp; &quot; &#39;";
        var result = WeiboParser.StripHtml(input);
        Assert.DoesNotContain("&amp;", result);
        Assert.DoesNotContain("&nbsp;", result);
        Assert.True(result.Length > 0);
    }

    // === MakeWeiboImageLarge ===

    [Fact]
    public void MakeWeiboImageLarge_ConvertsThumbnailToLarge()
    {
        var method = typeof(WeiboParser).GetMethod("MakeWeiboImageLarge", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var result = (string?)method?.Invoke(null, ["https://wx1.sinaimg.cn/thumbnail/abc.jpg"]);
        Assert.Contains("large", result);
        Assert.DoesNotContain("thumbnail", result);
    }

    // === CanParse ===

    [Fact]
    public void CanParse_WeiboPlatform_ReturnsTrue()
    {
        var parser = new WeiboParser();
        Assert.True(parser.CanParse(Platform.weibo, "https://weibo.com/status/123"));
    }

    [Fact]
    public void CanParse_DouyinPlatform_ReturnsFalse()
    {
        var parser = new WeiboParser();
        Assert.False(parser.CanParse(Platform.douyin, "https://v.douyin.com/abc"));
    }

    // === ParseWeiboJSON fixture test ===

    [Fact]
    public void ParseWeiboJson_ExtractsAllFields()
    {
        var json = """
        {
          "text": "这是微博正文 <br/>换行 &amp;特殊字符",
          "pics": [
            { "large": { "url": "https://wx1.sinaimg.cn/large/abc.jpg" } },
            { "url": "https://wx1.sinaimg.cn/thumbnail/def.jpg" }
          ],
          "user": {
            "screen_name": "测试用户",
            "id_str": "1234567890"
          },
          "created_at": "Mon Jun 24 12:00:00 +0800 2026"
        }
        """;

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        // text
        Assert.Equal("这是微博正文 <br/>换行 &amp;特殊字符", root.GetProperty("text").GetString());

        // pics
        var pics = root.GetProperty("pics").EnumerateArray().ToList();
        Assert.Equal(2, pics.Count);
        Assert.Equal("https://wx1.sinaimg.cn/large/abc.jpg", pics[0].GetProperty("large").GetProperty("url").GetString());
        Assert.Equal("https://wx1.sinaimg.cn/thumbnail/def.jpg", pics[1].GetProperty("url").GetString());

        // user
        Assert.Equal("测试用户", root.GetProperty("user").GetProperty("screen_name").GetString());
        Assert.Equal("1234567890", root.GetProperty("user").GetProperty("id_str").GetString());

        // created_at
        Assert.Equal("Mon Jun 24 12:00:00 +0800 2026", root.GetProperty("created_at").GetString());
    }

    // === Desktop uid/statusId format ===

    [Fact]
    public void CanParse_DesktopUidStatusIdUrl_ReturnsTrue()
    {
        var parser = new WeiboParser();
        Assert.True(parser.CanParse(Platform.weibo, "https://weibo.com/3064209773/5312436669776152"));
    }

    [Fact]
    public void ExtractContentId_DesktopUidStatusIdUrl_ReturnsStatusId()
    {
        var id = UrlNormalizer.ExtractContentId("https://weibo.com/3064209773/5312436669776152", Platform.weibo);
        Assert.Equal("5312436669776152", id);
    }

    [Fact]
    public void ExtractWeiboId_DesktopUidStatusIdUrl_ReturnsStatusId()
    {
        var id = UrlNormalizer.ExtractWeiboId("https://weibo.com/3064209773/5312436669776152");
        Assert.Equal("5312436669776152", id);
    }

    [Fact]
    public void Router_DesktopUidStatusIdUrl_UsesWeiboParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.weibo, "https://weibo.com/3064209773/5312436669776152");
        Assert.IsType<WeiboParser>(parser);
    }

    // === URL normalize ===

    [Fact]
    public void UrlNormalizer_NormalizesWeiboUrl()
    {
        var result = UrlNormalizer.Normalize("https://weibo.com/status/5155949834992299", Platform.weibo);
        Assert.Equal("weibo://status/5155949834992299", result);
    }
}
