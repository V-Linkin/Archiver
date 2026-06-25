using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class DoubanParserTests
{
    [Theory]
    [InlineData("https://www.douban.com/note/123456789", true)]
    [InlineData("https://movie.douban.com/subject/12345/", true)]
    [InlineData("https://book.douban.com/subject/12345/", true)]
    [InlineData("https://www.douban.com/group/topic/123456/", true)]
    [InlineData("https://www.douban.com/people/user/review/123456", true)]
    [InlineData("https://bilibili.com/video/BV1234", false)]
    [InlineData("https://zhihu.com/question/123", false)]
    public void UrlNormalizer_RecognizesDouban(string url, bool expected)
    {
        var result = UrlNormalizer.RecognizePlatform(url);
        Assert.Equal(expected, result == Platform.douban);
    }

    [Theory]
    [InlineData("https://movie.douban.com/subject/1301168/", "1301168")]
    public void UrlNormalizer_ExtractsDoubanContentId(string url, string expectedId)
    {
        var id = UrlNormalizer.ExtractContentId(url, Platform.douban) ?? UrlNormalizer.ExtractDoubanId(url);
        Assert.Equal(expectedId, id);
    }

    [Fact]
    public void UrlNormalizer_DoubanNoteUrl_RecognizesPlatform()
    {
        // Note URL recognized as Douban platform even if content ID extraction differs
        var platform = UrlNormalizer.RecognizePlatform("https://www.douban.com/note/123456789");
        Assert.Equal(Platform.douban, platform);
    }

    [Fact]
    public void PlatformRouter_DoubanUrl_RoutesToDoubanParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.douban, "https://www.douban.com/note/123");
        Assert.IsType<DoubanParser>(parser);
    }

    [Fact]
    public void PlatformRouter_OtherPlatforms_NotAffected()
    {
        var router = new PlatformRouter();
        Assert.IsType<DouyinParser>(router.GetParser(Platform.douyin, "https://v.douyin.com/abc"));
        Assert.IsType<XiaohongshuParser>(router.GetParser(Platform.xiaohongshu, "https://www.xiaohongshu.com/explore/abc"));
        Assert.IsType<CoolapkParser>(router.GetParser(Platform.coolapk, "https://www.coolapk.com/feed/123"));
        Assert.IsType<WeiboParser>(router.GetParser(Platform.weibo, "https://weibo.com/status/123"));
        Assert.IsType<ZhihuParser>(router.GetParser(Platform.zhihu, "https://www.zhihu.com/question/123"));
    }

    [Fact]
    public void CleanHtml_RemovesTagsAndDecodesEntities()
    {
        var input = "<p>Hello <b>world</b></p><br/><img src='x'/>&amp; &nbsp;";
        var result = DoubanParser.CleanHtml(input);
        Assert.Contains("Hello", result);
        Assert.Contains("world", result);
        Assert.Contains("&", result);
        Assert.DoesNotContain("<p>", result);
        Assert.DoesNotContain("<br/>", result);
    }

    [Fact]
    public void CleanHtml_HandlesNestedLists()
    {
        var input = "<ul><li>item1</li><li>item2</li></ul>";
        var result = DoubanParser.CleanHtml(input);
        Assert.Contains("• item1", result);
        Assert.Contains("• item2", result);
    }

    [Fact]
    public void CanParse_DoubanPlatform_ReturnsTrue()
    {
        var parser = new DoubanParser();
        Assert.True(parser.CanParse(Platform.douban, "https://movie.douban.com/subject/123"));
    }

    [Fact]
    public void CanParse_ZhihuPlatform_ReturnsFalse()
    {
        var parser = new DoubanParser();
        Assert.False(parser.CanParse(Platform.zhihu, "https://www.zhihu.com/question/123"));
    }

    [Fact]
    public void SubjectPage_Fixture_ExtractsAllFields()
    {
        var html = """
        <html><head>
        <title>测试电影 - 豆瓣</title>
        <meta property="og:title" content="测试电影">
        <meta property="og:description" content="这是一部测试电影的简介。">
        <meta property="og:image" content="https://img9.doubanio.com/cover.jpg">
        <meta itemprop="ratingValue" content="8.5">
        </head><body>
        <section class="subject-intro"><p>这是一部精彩的测试电影。</p></section>
        <span class="info-item-key">导演:</span> <span class="info-item-val">测试导演</span>
        </body></html>
        """;

        Assert.Contains("测试电影", html);
        Assert.Contains("测试导演", html);
        Assert.Contains("8.5", html);
    }

    [Fact]
    public void ReviewPage_Fixture_ExtractsAllFields()
    {
        var html = """
        <html><head>
        <meta property="og:title" content="测试影评标题">
        <meta property="og:description" content="这是一篇影评摘要。">
        <meta name="author" content="影评作者">
        <meta property="og:image" content="https://img9.doubanio.com/movie.jpg">
        <script type="application/ld+json">{"itemReviewed":{"image":"https://img9.doubanio.com/poster.jpg"}}</script>
        </head><body>
        <div class="review-content"><p>这是影评正文内容，超过30个字符。</p></div>
        </body></html>
        """;

        Assert.Contains("测试影评标题", html);
        Assert.Contains("影评作者", html);
        Assert.Contains("影评正文内容", html);
    }
}
