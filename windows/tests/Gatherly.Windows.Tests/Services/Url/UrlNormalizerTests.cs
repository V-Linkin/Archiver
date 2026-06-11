using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Url;
using Xunit;

namespace Gatherly.Windows.Tests.Services.Url;

public class UrlNormalizerTests
{
    [Fact]
    public void ExtractUrls_EmptyString_ReturnsEmpty()
    {
        var result = UrlNormalizer.ExtractUrls("");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractUrls_Whitespace_ReturnsEmpty()
    {
        var result = UrlNormalizer.ExtractUrls("   ");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractUrls_PlainText_ReturnsEmpty()
    {
        var result = UrlNormalizer.ExtractUrls("这是一段普通文字");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractUrls_MixedText_ExtractsUrl()
    {
        var result = UrlNormalizer.ExtractUrls("看看这个：https://github.com/openai/openai-dotnet");
        Assert.Single(result);
        Assert.Equal("https://github.com/openai/openai-dotnet", result[0]);
    }

    [Fact]
    public void ExtractUrls_MultipleUrls_ExtractsAll()
    {
        var result = UrlNormalizer.ExtractUrls(
            "https://github.com/a/b 和 https://youtube.com/watch?v=dQw4w9WgXcQ");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ExtractUrls_NoSchemeButDomain_Recognizes()
    {
        var result = UrlNormalizer.ExtractUrls("github.com/owner/repo");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractUrls_UnknownPlatform_NotIncluded()
    {
        var result = UrlNormalizer.ExtractUrls("https://www.example.com/page");
        Assert.Empty(result);
    }

    [Fact]
    public void ExtractFirstUrl_ReturnsFirstSupported()
    {
        var result = UrlNormalizer.ExtractFirstUrl(
            "https://example.com/page https://github.com/a/b");
        Assert.Equal("https://github.com/a/b", result);
    }

    [Fact]
    public void IsValidUrl_HttpUrl_ReturnsTrue()
    {
        Assert.True(UrlNormalizer.IsValidUrl("http://example.com"));
    }

    [Fact]
    public void IsValidUrl_HttpsUrl_ReturnsTrue()
    {
        Assert.True(UrlNormalizer.IsValidUrl("https://example.com"));
    }

    [Fact]
    public void IsValidUrl_NoScheme_ReturnsTrue()
    {
        Assert.True(UrlNormalizer.IsValidUrl("github.com/owner/repo"));
    }

    [Fact]
    public void IsValidUrl_Empty_ReturnsFalse()
    {
        Assert.False(UrlNormalizer.IsValidUrl(""));
    }

    [Fact]
    public void IsValidUrl_PlainText_ReturnsFalse()
    {
        Assert.False(UrlNormalizer.IsValidUrl("not a url"));
    }

    [Fact]
    public void RecognizePlatform_GitHub_ReturnsGitHub()
    {
        Assert.Equal(Platform.github, UrlNormalizer.RecognizePlatform("https://github.com/octocat/Hello-World"));
    }

    [Fact]
    public void RecognizePlatform_YouTubeWatch_ReturnsYouTube()
    {
        Assert.Equal(Platform.youtube, UrlNormalizer.RecognizePlatform("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
    }

    [Fact]
    public void RecognizePlatform_YoutuBe_ReturnsYouTube()
    {
        Assert.Equal(Platform.youtube, UrlNormalizer.RecognizePlatform("https://youtu.be/dQw4w9WgXcQ"));
    }

    [Fact]
    public void RecognizePlatform_YouTubeShorts_ReturnsYouTube()
    {
        Assert.Equal(Platform.youtube, UrlNormalizer.RecognizePlatform("https://www.youtube.com/shorts/dQw4w9WgXcQ"));
    }

    [Fact]
    public void RecognizePlatform_Bilibili_ReturnsBilibili()
    {
        Assert.Equal(Platform.bilibili, UrlNormalizer.RecognizePlatform("https://www.bilibili.com/video/BV1xx411c7mD"));
    }

    [Fact]
    public void RecognizePlatform_B23TV_ReturnsBilibili()
    {
        Assert.Equal(Platform.bilibili, UrlNormalizer.RecognizePlatform("https://b23.tv/abc123"));
    }

    [Fact]
    public void RecognizePlatform_XTweet_ReturnsX()
    {
        Assert.Equal(Platform.x, UrlNormalizer.RecognizePlatform("https://x.com/user/status/1234567890"));
    }

    [Fact]
    public void RecognizePlatform_Twitter_ReturnsX()
    {
        Assert.Equal(Platform.x, UrlNormalizer.RecognizePlatform("https://twitter.com/user/status/1234567890"));
    }

    [Fact]
    public void RecognizePlatform_Xiaohongshu_ReturnsXiaohongshu()
    {
        Assert.Equal(Platform.xiaohongshu, UrlNormalizer.RecognizePlatform("https://www.xiaohongshu.com/explore/65a1b2c3"));
    }

    [Fact]
    public void RecognizePlatform_XhsLink_ReturnsXiaohongshu()
    {
        Assert.Equal(Platform.xiaohongshu, UrlNormalizer.RecognizePlatform("https://xhslink.com/abc123"));
    }

    [Fact]
    public void RecognizePlatform_Douyin_ReturnsDouyin()
    {
        Assert.Equal(Platform.douyin, UrlNormalizer.RecognizePlatform("https://www.douyin.com/video/7301234567890"));
    }

    [Fact]
    public void RecognizePlatform_VDouyin_ReturnsDouyin()
    {
        Assert.Equal(Platform.douyin, UrlNormalizer.RecognizePlatform("https://v.douyin.com/abc123"));
    }

    [Fact]
    public void RecognizePlatform_Weibo_ReturnsWeibo()
    {
        Assert.Equal(Platform.weibo, UrlNormalizer.RecognizePlatform("https://weibo.com/status/4892046789012"));
    }

    [Fact]
    public void RecognizePlatform_Zhihu_ReturnsZhihu()
    {
        Assert.Equal(Platform.zhihu, UrlNormalizer.RecognizePlatform("https://www.zhihu.com/question/12345678/answer/87654321"));
    }

    [Fact]
    public void RecognizePlatform_Douban_ReturnsDouban()
    {
        Assert.Equal(Platform.douban, UrlNormalizer.RecognizePlatform("https://movie.douban.com/subject/35517853/"));
    }

    [Fact]
    public void RecognizePlatform_Coolapk_ReturnsCoolapk()
    {
        Assert.Equal(Platform.coolapk, UrlNormalizer.RecognizePlatform("https://www.coolapk.com/feed/12345678"));
    }

    [Fact]
    public void RecognizePlatform_Unknown_ReturnsNull()
    {
        Assert.Null(UrlNormalizer.RecognizePlatform("https://www.example.com/article"));
    }

    [Fact]
    public void ExtractYouTubeId_Watch_ReturnsId()
    {
        Assert.Equal("dQw4w9WgXcQ", UrlNormalizer.ExtractYouTubeId("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
    }

    [Fact]
    public void ExtractYouTubeId_Short_ReturnsId()
    {
        Assert.Equal("dQw4w9WgXcQ", UrlNormalizer.ExtractYouTubeId("https://youtu.be/dQw4w9WgXcQ"));
    }

    [Fact]
    public void ExtractYouTubeId_Shorts_ReturnsId()
    {
        Assert.Equal("dQw4w9WgXcQ", UrlNormalizer.ExtractYouTubeId("https://www.youtube.com/shorts/dQw4w9WgXcQ"));
    }

    [Fact]
    public void ExtractYouTubeId_Embed_ReturnsId()
    {
        Assert.Equal("dQw4w9WgXcQ", UrlNormalizer.ExtractYouTubeId("https://www.youtube.com/embed/dQw4w9WgXcQ"));
    }

    [Fact]
    public void ExtractYouTubeId_Handle_ReturnsHandle()
    {
        Assert.Equal("@mkbhd", UrlNormalizer.ExtractYouTubeId("https://www.youtube.com/@mkbhd"));
    }

    [Fact]
    public void ExtractYouTubeId_Channel_ReturnsId()
    {
        Assert.Equal("UCxxxxxxx", UrlNormalizer.ExtractYouTubeId("https://www.youtube.com/channel/UCxxxxxxx"));
    }

    [Fact]
    public void ExtractBilibiliBV_ReturnsBV()
    {
        Assert.Equal("BV1xx411c7mD", UrlNormalizer.ExtractBilibiliBV("https://www.bilibili.com/video/BV1xx411c7mD"));
    }

    [Fact]
    public void ExtractBilibiliBV_Av_ReturnsAv()
    {
        Assert.Equal("av170001", UrlNormalizer.ExtractBilibiliBV("https://www.bilibili.com/video/av170001"));
    }

    [Fact]
    public void ExtractXId_ReturnsId()
    {
        Assert.Equal("1234567890", UrlNormalizer.ExtractXId("https://x.com/user/status/1234567890"));
    }

    [Fact]
    public void ExtractXId_Twitter_ReturnsId()
    {
        Assert.Equal("1234567890", UrlNormalizer.ExtractXId("https://twitter.com/user/status/1234567890"));
    }

    [Fact]
    public void ExtractXId_IStatus_ReturnsId()
    {
        Assert.Equal("1234567890", UrlNormalizer.ExtractXId("https://x.com/i/status/1234567890"));
    }

    [Fact]
    public void ExtractXUsername_ReturnsUsername()
    {
        Assert.Equal("user", UrlNormalizer.ExtractXUsername("https://x.com/user/status/1234567890"));
    }

    [Fact]
    public void ExtractXUsername_ProfilePage_ReturnsUsername()
    {
        Assert.Equal("user", UrlNormalizer.ExtractXUsername("https://x.com/user"));
    }

    [Fact]
    public void ExtractContentId_GitHub_ReturnsOwnerRepo()
    {
        Assert.Equal("octocat/Hello-World",
            UrlNormalizer.ExtractContentId("https://github.com/octocat/Hello-World", Platform.github));
    }

    [Fact]
    public void ExtractContentId_Douyin_ReturnsNumericId()
    {
        Assert.Equal("7301234567890",
            UrlNormalizer.ExtractContentId("https://www.douyin.com/video/7301234567890", Platform.douyin));
    }

    [Fact]
    public void ExtractContentId_Xiaohongshu_ReturnsHexId()
    {
        Assert.Equal("65a1b2c3d4e5f6789012345",
            UrlNormalizer.ExtractContentId("https://www.xiaohongshu.com/explore/65a1b2c3d4e5f6789012345", Platform.xiaohongshu));
    }

    [Fact]
    public void ExtractContentId_Coolapk_ReturnsNumericId()
    {
        Assert.Equal("12345678",
            UrlNormalizer.ExtractContentId("https://www.coolapk.com/feed/12345678", Platform.coolapk));
    }

    [Fact]
    public void ExtractContentId_Weibo_ReturnsId()
    {
        Assert.Equal("4892046789012",
            UrlNormalizer.ExtractContentId("https://weibo.com/status/4892046789012", Platform.weibo));
    }

    [Fact]
    public void ExtractContentId_Zhihu_Answer_ReturnsId()
    {
        Assert.Equal("87654321",
            UrlNormalizer.ExtractContentId("https://www.zhihu.com/question/12345678/answer/87654321", Platform.zhihu));
    }

    [Fact]
    public void ExtractContentId_Zhihu_Post_ReturnsId()
    {
        Assert.Equal("12345678",
            UrlNormalizer.ExtractContentId("https://zhuanlan.zhihu.com/p/12345678", Platform.zhihu));
    }

    [Fact]
    public void ExtractContentId_Douban_ReturnsId()
    {
        Assert.Equal("35517853",
            UrlNormalizer.ExtractContentId("https://movie.douban.com/subject/35517853/", Platform.douban));
    }

    [Fact]
    public void ExtractContentId_XhsLink_ReturnsNull()
    {
        Assert.Null(UrlNormalizer.ExtractContentId("https://xhslink.com/abc123", Platform.xiaohongshu));
    }

    [Fact]
    public void ExtractContentId_B23TV_ReturnsNull()
    {
        Assert.Null(UrlNormalizer.ExtractContentId("https://b23.tv/abc123", Platform.bilibili));
    }

    [Fact]
    public void Normalize_Douyin_ReturnsCanonicalScheme()
    {
        Assert.Equal("douyin://video/7301234567890",
            UrlNormalizer.Normalize("https://www.douyin.com/video/7301234567890?from=share", Platform.douyin));
    }

    [Fact]
    public void Normalize_Bilibili_ReturnsCanonicalScheme()
    {
        Assert.Equal("bilibili://video/BV1xx411c7mD",
            UrlNormalizer.Normalize("https://www.bilibili.com/video/BV1xx411c7mD?spm_id_from=333", Platform.bilibili));
    }

    [Fact]
    public void Normalize_YouTube_ReturnsCanonicalScheme()
    {
        Assert.Equal("youtube://video/dQw4w9WgXcQ",
            UrlNormalizer.Normalize("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL", Platform.youtube));
    }

    [Fact]
    public void Normalize_GitHub_ReturnsCanonicalScheme()
    {
        Assert.Equal("github://repo/octocat/Hello-World",
            UrlNormalizer.Normalize("https://github.com/octocat/Hello-World", Platform.github));
    }

    [Fact]
    public void Normalize_X_ReturnsCanonicalScheme()
    {
        Assert.Equal("x://tweet/1234567890",
            UrlNormalizer.Normalize("https://x.com/user/status/1234567890", Platform.x));
    }

    [Fact]
    public void Normalize_XhsLink_ReturnsOriginalUrl()
    {
        var url = "https://xhslink.com/abc123";
        Assert.Equal(url, UrlNormalizer.Normalize(url, Platform.xiaohongshu));
    }

    [Fact]
    public void Normalize_B23TV_ReturnsOriginalUrl()
    {
        var url = "https://b23.tv/abc123";
        Assert.Equal(url, UrlNormalizer.Normalize(url, Platform.bilibili));
    }

    [Fact]
    public void Normalize_UnknownPlatform_ReturnsOriginalUrl()
    {
        var url = "https://www.example.com/page";
        Assert.Equal(url, UrlNormalizer.Normalize(url, Platform.custom));
    }

    [Fact]
    public void DedupConsistency_Douyin_SameNormalizedUrl()
    {
        var urls = new[]
        {
            "https://www.douyin.com/video/7301234567890",
            "https://www.douyin.com/video/7301234567890?from_share=single",
            "https://www.iesdouyin.com/share/video/7301234567890"
        };
        var normalized = urls.Select(u => UrlNormalizer.Normalize(u, Platform.douyin)).Distinct().ToList();
        Assert.Single(normalized);
        Assert.Equal("douyin://video/7301234567890", normalized[0]);
    }

    [Fact]
    public void DedupConsistency_Bilibili_SameNormalizedUrl()
    {
        var urls = new[]
        {
            "https://www.bilibili.com/video/BV1xx411c7mD",
            "https://bilibili.com/video/BV1xx411c7mD?spm_id_from=333.337.0.0"
        };
        var normalized = urls.Select(u => UrlNormalizer.Normalize(u, Platform.bilibili)).Distinct().ToList();
        Assert.Single(normalized);
        Assert.Equal("bilibili://video/BV1xx411c7mD", normalized[0]);
    }

    [Theory]
    [InlineData("https://www.douyin.com/video/7301234567890", "douyin://video/7301234567890")]
    [InlineData("https://www.xiaohongshu.com/explore/65a1b2c3d4e5f6789012345", "xiaohongshu://explore/65a1b2c3d4e5f6789012345")]
    [InlineData("https://www.coolapk.com/feed/12345678", "coolapk://feed/12345678")]
    [InlineData("https://www.bilibili.com/video/BV1xx411c7mD", "bilibili://video/BV1xx411c7mD")]
    [InlineData("https://github.com/octocat/Hello-World", "github://repo/octocat/Hello-World")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "youtube://video/dQw4w9WgXcQ")]
    [InlineData("https://x.com/user/status/1234567890", "x://tweet/1234567890")]
    [InlineData("https://weibo.com/status/4892046789012", "weibo://status/4892046789012")]
    [InlineData("https://www.zhihu.com/question/12345678/answer/87654321", "zhihu://content/87654321")]
    [InlineData("https://movie.douban.com/subject/35517853/", "douban://subject/35517853")]
    public void Normalize_KnownPlatforms_ReturnsExpected(string input, string expected)
    {
        var platform = UrlNormalizer.RecognizePlatform(input);
        Assert.NotNull(platform);
        Assert.Equal(expected, UrlNormalizer.Normalize(input, platform.Value));
    }
}
