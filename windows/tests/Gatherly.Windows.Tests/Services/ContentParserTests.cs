using Gatherly.Windows.Services;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class ContentParserTests
{
    [Fact]
    public void ParseSegments_HttpsUrl_Recognized()
    {
        var segments = ContentParser.ParseSegments("Visit https://example.com for more");
        Assert.Equal(3, segments.Count);
        Assert.False(segments[0].IsLink);
        Assert.True(segments[1].IsLink);
        Assert.Equal("https://example.com", segments[1].Url);
        Assert.False(segments[2].IsLink);
    }

    [Fact]
    public void ParseSegments_HttpUrl_Recognized()
    {
        var segments = ContentParser.ParseSegments("Visit http://example.com for more");
        Assert.Equal(3, segments.Count);
        Assert.True(segments[1].IsLink);
        Assert.Equal("http://example.com", segments[1].Url);
    }

    [Fact]
    public void ParseSegments_MidLineUrl_Recognized()
    {
        var segments = ContentParser.ParseSegments("Check https://example.com now");
        Assert.Equal(3, segments.Count);
        Assert.True(segments[1].IsLink);
    }

    [Fact]
    public void ParseSegments_MultipleUrls_AllRecognized()
    {
        var segments = ContentParser.ParseSegments("Visit https://a.com and https://b.com");
        var links = segments.Where(s => s.IsLink).ToList();
        Assert.Equal(2, links.Count);
        Assert.Equal("https://a.com", links[0].Url);
        Assert.Equal("https://b.com", links[1].Url);
    }

    [Fact]
    public void ParseSegments_UrlWithEnglishPeriod_ExcludesPeriod()
    {
        var segments = ContentParser.ParseSegments("Visit https://example.com.");
        var link = segments.FirstOrDefault(s => s.IsLink);
        Assert.NotNull(link);
        Assert.Equal("https://example.com", link!.Url);
    }

    [Fact]
    public void ParseSegments_UrlWithChinesePeriod_ExcludesPeriod()
    {
        var segments = ContentParser.ParseSegments("访问 https://example.com。");
        var link = segments.FirstOrDefault(s => s.IsLink);
        Assert.NotNull(link);
        Assert.Equal("https://example.com", link!.Url);
    }

    [Fact]
    public void ParseSegments_UrlWithRightParen_ExcludesParen()
    {
        var segments = ContentParser.ParseSegments("See (https://example.com)");
        var link = segments.FirstOrDefault(s => s.IsLink);
        Assert.NotNull(link);
        Assert.Equal("https://example.com", link!.Url);
    }

    [Fact]
    public void ParseSegments_PlainText_KeptAsIs()
    {
        var segments = ContentParser.ParseSegments("Just plain text");
        Assert.Single(segments);
        Assert.False(segments[0].IsLink);
        Assert.Equal("Just plain text", segments[0].Text);
    }

    [Fact]
    public void ParseSegments_EmptyText_ReturnsEmpty()
    {
        var segments = ContentParser.ParseSegments("");
        Assert.Empty(segments);
    }

    [Fact]
    public void ParseSegments_NullText_ReturnsEmpty()
    {
        var segments = ContentParser.ParseSegments(null);
        Assert.Empty(segments);
    }

    [Fact]
    public void ParseSegments_WhitespaceText_ReturnsEmpty()
    {
        var segments = ContentParser.ParseSegments("   ");
        Assert.Empty(segments);
    }

    [Fact]
    public void ParseSegments_Newlines_Preserved()
    {
        var text = "Line 1\nLine 2";
        var segments = ContentParser.ParseSegments(text);
        Assert.Single(segments);
        Assert.Contains("\n", segments[0].Text);
    }

    [Fact]
    public void ParseSegments_UrlWithQueryParams_Correct()
    {
        var segments = ContentParser.ParseSegments("https://example.com?foo=bar&baz=1");
        var link = segments.FirstOrDefault(s => s.IsLink);
        Assert.NotNull(link);
        Assert.Equal("https://example.com?foo=bar&baz=1", link!.Url);
    }

    [Fact]
    public void ParseSegments_UrlWithFragment_Correct()
    {
        var segments = ContentParser.ParseSegments("https://example.com#section");
        var link = segments.FirstOrDefault(s => s.IsLink);
        Assert.NotNull(link);
        Assert.Equal("https://example.com#section", link!.Url);
    }
}
