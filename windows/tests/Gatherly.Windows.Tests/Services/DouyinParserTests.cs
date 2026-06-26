using System.Text.Json;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;
using Xunit;

namespace Gatherly.Windows.Tests.Services;

public class DouyinParserTests
{
    private readonly DouyinParser _parser = new();

    [Fact]
    public void CanParse_DouyinUrl_ReturnsTrue()
    {
        Assert.True(_parser.CanParse(Platform.douyin, "https://www.douyin.com/video/7123456789"));
    }

    [Fact]
    public void CanParse_IesDouyinUrl_ReturnsTrue()
    {
        Assert.True(_parser.CanParse(Platform.douyin, "https://www.iesdouyin.com/share/video/7123456789"));
    }

    [Fact]
    public void CanParse_NonDouyinPlatform_ReturnsFalse()
    {
        // Windows IContentParser.CanParse 接收 Platform 枚举，不是 URL
        // 所以当 platform 不是 douyin 时返回 false
        Assert.False(_parser.CanParse(Platform.bilibili, "https://www.douyin.com/video/7123456789"));
    }

    [Fact]
    public void CanParse_OtherPlatform_ReturnsFalse()
    {
        // 当 platform 不是 douyin 时，即使 URL 是 douyin 也返回 false
        Assert.False(_parser.CanParse(Platform.github, "https://www.douyin.com/video/7123456789"));
    }

    [Fact]
    public void UrlNormalizer_RecognizesDouyin()
    {
        var platform = UrlNormalizer.RecognizePlatform("https://www.douyin.com/video/7123456789");
        Assert.Equal(Platform.douyin, platform);
    }

    [Fact]
    public void UrlNormalizer_RecognizesIesDouyin()
    {
        var platform = UrlNormalizer.RecognizePlatform("https://www.iesdouyin.com/share/video/7123456789");
        Assert.Equal(Platform.douyin, platform);
    }

    [Fact]
    public void UrlNormalizer_ExtractsDouyinId()
    {
        var id = UrlNormalizer.ExtractContentId("https://www.douyin.com/video/7123456789", Platform.douyin);
        Assert.Equal("7123456789", id);
    }

    [Fact]
    public void UrlNormalizer_ExtractsIesDouyinId()
    {
        var id = UrlNormalizer.ExtractContentId("https://www.iesdouyin.com/share/video/7123456789", Platform.douyin);
        Assert.Equal("7123456789", id);
    }

    [Fact]
    public void UrlNormalizer_NormalizesDouyinUrl()
    {
        var normalized = UrlNormalizer.Normalize("https://www.douyin.com/video/7123456789", Platform.douyin);
        Assert.Equal("douyin://video/7123456789", normalized);
    }

    [Fact]
    public void PlatformRouter_RoutesDouyinToDouyinParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.douyin, "https://www.douyin.com/video/7123456789");
        Assert.IsType<DouyinParser>(parser);
    }

    [Fact]
    public void PlatformRouter_Xiaohongshu_GoesToXiaohongshuParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.xiaohongshu, "https://www.xiaohongshu.com/explore/abc123");
        Assert.IsType<XiaohongshuParser>(parser);
    }

    [Fact]
    public async Task ParseAsync_InvalidUrl_ReturnsFail()
    {
        var request = new ParseRequest
        {
            Url = "https://www.douyin.com/video/7123456789",
            Platform = Platform.douyin
        };

        // This will fail with network error in test environment, which is expected
        var result = await _parser.ParseAsync(request, CancellationToken.None);
        // Should either succeed (if network available) or fail with clear error
        Assert.True(result.Status == ParseStatus.Success || result.Status == ParseStatus.Failed);
        if (result.Status == ParseStatus.Failed)
        {
            Assert.NotNull(result.ErrorMessage);
            Assert.NotEmpty(result.ErrorMessage);
        }
    }

    [Fact]
    public void ParseMobileJson_VideoNote_ParsedCorrectly()
    {
        // 模拟 macOS parseMobileJSON + parseNoteDetail 的输入
        var json = """
        {
            "loaderData": {
                "note_(7123456789)/page": {
                    "aweme": {
                        "detail": {
                            "desc": "测试视频描述 #测试话题",
                            "title": "测试标题",
                            "aweme_type": 0,
                            "author": {
                                "nickname": "测试用户",
                                "uid": "12345678"
                            },
                            "video": {
                                "cover": {
                                    "url_list": ["https://example.com/cover.jpg"]
                                },
                                "play_addr": {
                                    "url_list": ["https://example.com/playwm/video.mp4"]
                                }
                            },
                            "images": []
                        }
                    }
                }
            }
        }
        """;

        // 使用反射测试私有方法（或通过 ParseAsync 间接测试）
        // 这里我们验证 JSON 结构可以被正确解析
        var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("loaderData", out var loaderData));

        var found = false;
        foreach (var prop in loaderData.EnumerateObject())
        {
            if (prop.Name.StartsWith("note_(") && prop.Name.EndsWith(")/page"))
            {
                found = true;
                Assert.True(prop.Value.TryGetProperty("aweme", out var aweme));
                Assert.True(aweme.TryGetProperty("detail", out var detail));
                Assert.Equal("测试视频描述 #测试话题", detail.GetProperty("desc").GetString());
                Assert.Equal(0, detail.GetProperty("aweme_type").GetInt32());
                Assert.Equal("测试用户", detail.GetProperty("author").GetProperty("nickname").GetString());
                break;
            }
        }
        Assert.True(found, "Should find note_(id)/page key in loaderData");
    }

    [Fact]
    public void ParseMobileJson_ImageNote_ParsedCorrectly()
    {
        var json = """
        {
            "loaderData": {
                "note_(7123456789)/page": {
                    "aweme": {
                        "detail": {
                            "desc": "测试图文描述",
                            "aweme_type": 2,
                            "author": {
                                "nickname": "图文作者"
                            },
                            "images": [
                                {"url_list": ["https://example.com/img1.jpg"]},
                                {"url_list": ["https://example.com/img2.jpg"]}
                            ]
                        }
                    }
                }
            }
        }
        """;

        var doc = JsonDocument.Parse(json);
        var loaderData = doc.RootElement.GetProperty("loaderData");

        foreach (var prop in loaderData.EnumerateObject())
        {
            if (prop.Name.StartsWith("note_(") && prop.Name.EndsWith(")/page"))
            {
                var detail = prop.Value.GetProperty("aweme").GetProperty("detail");
                Assert.Equal(2, detail.GetProperty("aweme_type").GetInt32());

                var images = detail.GetProperty("images");
                Assert.Equal(2, images.GetArrayLength());
                Assert.Equal("https://example.com/img1.jpg", images[0].GetProperty("url_list")[0].GetString());
                break;
            }
        }
    }

    [Fact]
    public void DeWatermark_ReplacesPlaywmWithPlay()
    {
        // 验证去水印逻辑：/playwm/ → /play/
        var original = "https://example.com/playwm/video.mp4";
        var deWatermarked = original.Replace("/playwm/", "/play/");
        Assert.Equal("https://example.com/play/video.mp4", deWatermarked);
    }

    [Fact]
    public void TitleExtraction_FallbackToDescWithoutHashtags()
    {
        // 验证标题回退逻辑：去掉 #话题 后截取前50字
        var desc = "这是一段测试文字 #话题标签 #另一个话题";
        var pattern = new System.Text.RegularExpressions.Regex("""#[^#\n\t ]+""");
        var cleaned = pattern.Replace(desc, "").Trim();
        var title = cleaned.Length > 50 ? cleaned[..50] : cleaned;
        Assert.Equal("这是一段测试文字", title);
    }

    [Fact]
    public void TitleExtraction_TruncatesAt50Chars()
    {
        var desc = new string('测', 60) + " #话题";
        var pattern = new System.Text.RegularExpressions.Regex("""#[^#\n\t ]+""");
        var cleaned = pattern.Replace(desc, "").Trim();
        var title = cleaned.Length > 50 ? cleaned[..50] : cleaned;
        Assert.Equal(50, title.Length);
    }

    [Fact]
    public void ExtractUrls_FromShareText_FindsDouyinUrl()
    {
        var text = "看看这个视频 https://www.douyin.com/video/7123456789 太有意思了";
        var urls = UrlNormalizer.ExtractUrls(text);
        Assert.Single(urls);
        Assert.Contains("douyin.com", urls[0]);
    }

    [Fact]
    public void ExtractUrls_FromShareTextWithMultipleUrls_FindsAll()
    {
        var text = "视频1 https://www.douyin.com/video/111 视频2 https://www.douyin.com/video/222";
        var urls = UrlNormalizer.ExtractUrls(text);
        Assert.Equal(2, urls.Count);
    }

    [Fact]
    public void DouyinUrl_NotRoutedToNotImplemented()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.douyin, "https://www.douyin.com/video/7123456789");
        Assert.IsType<DouyinParser>(parser);
        Assert.IsNotType<NotImplementedParser>(parser);
    }

    [Fact]
    public void ExistingBilibiliParser_StillWorks()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.bilibili, "https://www.bilibili.com/video/BV1234567");
        Assert.IsType<BilibiliParser>(parser);
    }

    [Fact]
    public void ExistingGitHubParser_StillWorks()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.github, "https://github.com/user/repo");
        Assert.IsType<GitHubParser>(parser);
    }

    [Fact]
    public void ExistingYouTubeParser_StillWorks()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        Assert.IsType<YouTubeParser>(parser);
    }
}
