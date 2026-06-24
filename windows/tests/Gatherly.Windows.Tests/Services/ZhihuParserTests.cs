using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class ZhihuParserTests
{
    // === URL / Normalizer ===

    [Theory]
    [InlineData("https://www.zhihu.com/question/12345678/answer/87654321", true)]
    [InlineData("https://zhuanlan.zhihu.com/p/123456789", true)]
    [InlineData("https://zhihu.com/question/12345678/answer/87654321", true)]
    [InlineData("https://bilibili.com/video/BV1234", false)]
    [InlineData("https://weibo.com/status/123", false)]
    public void UrlNormalizer_RecognizesZhihu(string url, bool expected)
    {
        var result = UrlNormalizer.RecognizePlatform(url);
        Assert.Equal(expected, result == Platform.zhihu);
    }

    [Theory]
    [InlineData("https://www.zhihu.com/question/12345678/answer/87654321", "87654321")]
    [InlineData("https://zhuanlan.zhihu.com/p/123456789", "123456789")]
    public void UrlNormalizer_ExtractsZhihuContentId(string url, string expectedId)
    {
        var id = UrlNormalizer.ExtractContentId(url, Platform.zhihu);
        Assert.Equal(expectedId, id);
    }

    // === PlatformRouter ===

    [Fact]
    public void PlatformRouter_ZhihuUrl_RoutesToZhihuParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.zhihu, "https://www.zhihu.com/question/123/answer/456");
        Assert.IsType<ZhihuParser>(parser);
    }

    [Fact]
    public void PlatformRouter_OtherPlatforms_NotAffected()
    {
        var router = new PlatformRouter();
        Assert.IsType<DouyinParser>(router.GetParser(Platform.douyin, "https://v.douyin.com/abc"));
        Assert.IsType<XiaohongshuParser>(router.GetParser(Platform.xiaohongshu, "https://www.xiaohongshu.com/explore/abc"));
        Assert.IsType<CoolapkParser>(router.GetParser(Platform.coolapk, "https://www.coolapk.com/feed/123"));
        Assert.IsType<WeiboParser>(router.GetParser(Platform.weibo, "https://weibo.com/status/123"));
    }

    // === ConvertHtmlToMarkdown ===

    [Fact]
    public void ConvertHtmlToMarkdown_RemovesTagsAndConverts()
    {
        var input = "<p>Hello <b>world</b></p><br/><img src=\"https://example.com/img.jpg\"/>";
        var result = ZhihuParser.ConvertHtmlToMarkdown(input);
        Assert.Contains("Hello **world**", result);
        Assert.Contains("![](https://example.com/img.jpg)", result);
        Assert.DoesNotContain("<p>", result);
    }

    [Fact]
    public void ConvertHtmlToMarkdown_DecodesHTMLEntities()
    {
        var input = "&amp; &lt; &gt; &nbsp; &quot;";
        var result = ZhihuParser.ConvertHtmlToMarkdown(input);
        Assert.DoesNotContain("&amp;", result);
        Assert.Contains(" ", result);
    }

    // === ExtractImagesFromHtml ===

    [Fact]
    public void ExtractImagesFromHtml_FindsImageUrls()
    {
        var html = "<img data-original=\"https://example.com/a.jpg\"/><img src=\"https://example.com/b.png\"/>";
        var images = ZhihuParser.ExtractImagesFromHtml(html);
        Assert.Contains("https://example.com/a.jpg", images);
        Assert.Contains("https://example.com/b.png", images);
    }

    [Fact]
    public void ExtractImagesFromHtml_DeduplicatesUrls()
    {
        var html = "<img data-original=\"https://example.com/a.jpg\"/><img data-actualsrc=\"https://example.com/a.jpg\"/>";
        var images = ZhihuParser.ExtractImagesFromHtml(html);
        Assert.Single(images);
        Assert.Equal("https://example.com/a.jpg", images[0]);
    }

    // === CanParse ===

    [Fact]
    public void CanParse_ZhihuPlatform_ReturnsTrue()
    {
        var parser = new ZhihuParser();
        Assert.True(parser.CanParse(Platform.zhihu, "https://www.zhihu.com/question/123/answer/456"));
    }

    [Fact]
    public void CanParse_DouyinPlatform_ReturnsFalse()
    {
        var parser = new ZhihuParser();
        Assert.False(parser.CanParse(Platform.douyin, "https://v.douyin.com/abc"));
    }

    // === Answer API fixture test ===

    [Fact]
    public void AnswerApi_Fixture_ExtractsAllFields()
    {
        var json = """
        {
          "content": "<p>这是回答内容</p><img data-original=\"https://example.com/img1.jpg\"/>",
          "question": { "title": "测试问题" },
          "author": { "name": "测试作者", "url_token": "test_user" },
          "created_time": 1719206400.0,
          "content_need_truncated": false
        }
        """;

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("测试问题", root.GetProperty("question").GetProperty("title").GetString());
        Assert.Equal("测试作者", root.GetProperty("author").GetProperty("name").GetString());
        Assert.Equal("test_user", root.GetProperty("author").GetProperty("url_token").GetString());
        Assert.Contains("这是回答内容", root.GetProperty("content").GetString()!);
    }

    [Fact]
    public void ArticleApi_Fixture_ExtractsAllFields()
    {
        var json = """
        {
          "title": "测试文章标题",
          "content": "<p>文章正文</p>",
          "author": { "name": "文章作者", "url_token": "article_user" },
          "created": 1719206400.0
        }
        """;

        var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("测试文章标题", root.GetProperty("title").GetString());
        Assert.Equal("文章作者", root.GetProperty("author").GetProperty("name").GetString());
        Assert.Contains("文章正文", root.GetProperty("content").GetString()!);
    }
}
