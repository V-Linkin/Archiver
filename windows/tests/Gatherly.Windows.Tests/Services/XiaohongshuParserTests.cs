using System.Text.Json;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;
using Xunit;
using Xunit.Abstractions;

namespace Gatherly.Windows.Tests.Services;

public class XiaohongshuParserTests
{
    private readonly ITestOutputHelper _output;

    public XiaohongshuParserTests(ITestOutputHelper output) { _output = output; }

    // === URL / Normalizer ===

    [Theory]
    [InlineData("https://www.xiaohongshu.com/explore/6a1fc81a0000000006020444", true)]
    [InlineData("https://www.xiaohongshu.com/discovery/item/6a199cdd0000000035022bd2", true)]
    [InlineData("https://xhslink.com/abc123", true)]
    [InlineData("https://bilibili.com/video/BV1234", false)]
    [InlineData("https://youtube.com/watch?v=abc", false)]
    public void UrlNormalizer_RecognizesXiaohongshu(string url, bool expected)
    {
        var result = UrlNormalizer.RecognizePlatform(url);
        Assert.Equal(expected, result == Platform.xiaohongshu);
    }

    [Theory]
    [InlineData("https://www.xiaohongshu.com/explore/6a1fc81a0000000006020444", "6a1fc81a0000000006020444")]
    [InlineData("https://www.xiaohongshu.com/discovery/item/6a199cdd0000000035022bd2", "6a199cdd0000000035022bd2")]
    public void UrlNormalizer_ExtractsXiaohongshuContentId(string url, string expectedId)
    {
        var id = UrlNormalizer.ExtractContentId(url, Platform.xiaohongshu);
        Assert.Equal(expectedId, id);
    }

    [Theory]
    [InlineData("看看这个笔记 https://www.xiaohongshu.com/explore/6a1fc81a0000000006020444 好好看")]
    [InlineData("看看 https://xhslink.com/abc123")]
    public void UrlNormalizer_ExtractsXiaohongshuUrlFromShareText(string text)
    {
        var url = UrlNormalizer.ExtractFirstUrl(text);
        Assert.NotNull(url);
        Assert.Equal(Platform.xiaohongshu, UrlNormalizer.RecognizePlatform(url));
    }

    // === PlatformRouter ===

    [Fact]
    public void PlatformRouter_XiaohongshuUrl_RoutesToXiaohongshuParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.xiaohongshu, "https://www.xiaohongshu.com/explore/123");
        Assert.IsType<XiaohongshuParser>(parser);
    }

    [Fact]
    public void PlatformRouter_DouyinUrl_StillRoutesToDouyinParser()
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(Platform.douyin, "https://v.douyin.com/abc");
        Assert.IsType<DouyinParser>(parser);
    }

    [Theory]
    [InlineData(Platform.github)]
    [InlineData(Platform.bilibili)]
    [InlineData(Platform.youtube)]
    public void PlatformRouter_OtherPlatforms_NotAffected(Platform platform)
    {
        var router = new PlatformRouter();
        var parser = router.GetParser(platform, "https://example.com");
        Assert.False(parser is XiaohongshuParser);
        Assert.False(parser is NotImplementedParser);
    }

    // === Parser fixture test with __INITIAL_STATE__ ===

    [Fact]
    public void ParseAsync_InitialStateImageNote_ReturnsFullFields()
    {
        var initialState = """
        {
          "note": {
            "noteDetailMap": {
              "6a1fc81a0000000006020444": {
                "note": {
                  "title": "测试笔记标题",
                  "desc": "这是测试笔记正文内容",
                  "user": {
                    "nickName": "测试作者",
                    "userId": "user123"
                  },
                  "imageList": [
                    { "fileId": "notes_uhdr/img001", "urlDefault": "https://example.com/img1.jpg" },
                    { "fileId": "notes_uhdr/img002", "urlDefault": "https://example.com/img2.jpg" }
                  ]
                }
              }
            }
          },
          "noteData": {
            "normalNotePreloadData": {
              "imagesList": [
                { "urlSizeLarge": "https://example.com/cover.jpg" }
              ]
            }
          }
        }
        """;
        var html = $"<html><script>window.__INITIAL_STATE__={initialState}</script></html>";

        var match = XiaohongshuParser.InitialStatePatternForTest(html);
        Assert.True(match.Success);

        var jsonStr = match.Groups[1].Value.Trim().TrimEnd(';');
        jsonStr = jsonStr.Replace("undefined", "null");
        var doc = JsonDocument.Parse(jsonStr);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("note", out var noteEl));
        Assert.True(noteEl.TryGetProperty("noteDetailMap", out var map));
        var first = map.EnumerateObject().First().Value;
        Assert.True(first.TryGetProperty("note", out var note));

        Assert.Equal("测试笔记标题", note.GetProperty("title").GetString());
        Assert.Equal("这是测试笔记正文内容", note.GetProperty("desc").GetString());

        var user = note.GetProperty("user");
        Assert.Equal("测试作者", user.GetProperty("nickName").GetString());
        Assert.Equal("user123", user.GetProperty("userId").GetString());

        var images = note.GetProperty("imageList").EnumerateArray().ToList();
        Assert.Equal(2, images.Count);
        Assert.Equal("notes_uhdr/img001", images[0].GetProperty("fileId").GetString());

        var preload = root.GetProperty("noteData").GetProperty("normalNotePreloadData").GetProperty("imagesList");
        Assert.Equal("https://example.com/cover.jpg", preload[0].GetProperty("urlSizeLarge").GetString());
    }

    [Fact]
    public void ParseAsync_InitialStateVideoNote_ReturnsVideoUrl()
    {
        var initialState = """
        {
          "note": {
            "noteDetailMap": {
              "abc123": {
                "note": {
                  "title": "视频笔记",
                  "desc": "视频描述",
                  "user": { "nickName": "视频作者" },
                  "video": {
                    "media": {
                      "stream": {
                        "h264": [
                          { "width": 1080, "masterUrl": "https://example.com/video_h264.mp4" }
                        ],
                        "h265": [
                          { "width": 720, "masterUrl": "https://example.com/video_h265.mp4" }
                        ]
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;
        var html = $"<html><script>window.__INITIAL_STATE__={initialState}</script></html>";

        var match = XiaohongshuParser.InitialStatePatternForTest(html);
        Assert.True(match.Success);

        // Parse and verify video URL
        var jsonStr = match.Groups[1].Value.Trim().TrimEnd(';');
        jsonStr = jsonStr.Replace("undefined", "null");
        var doc = JsonDocument.Parse(jsonStr);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("note", out var noteEl));
        Assert.True(noteEl.TryGetProperty("noteDetailMap", out var map));
        var first = map.EnumerateObject().First().Value;
        Assert.True(first.TryGetProperty("note", out var note));
        Assert.True(note.TryGetProperty("video", out var video));
        Assert.True(video.TryGetProperty("media", out var media));
        Assert.True(media.TryGetProperty("stream", out var stream));
        Assert.True(stream.TryGetProperty("h264", out var h264));
        var h264Arr = h264.EnumerateArray().ToList();
        Assert.Single(h264Arr);
        var masterUrl = h264Arr[0].GetProperty("masterUrl").GetString();
        Assert.Equal("https://example.com/video_h264.mp4", masterUrl);
    }

    [Fact]
    public void ParseAsync_MissingInitialState_ReturnsFail()
    {
        var html = "<html><body>No initial state here</body></html>";
        Assert.False(html.Contains("__INITIAL_STATE__="));
    }

    // === CanParse ===

    [Fact]
    public void CanParse_XiaohongshuPlatform_ReturnsTrue()
    {
        var parser = new XiaohongshuParser();
        Assert.True(parser.CanParse(Platform.xiaohongshu, "https://www.xiaohongshu.com/explore/123"));
    }

    [Fact]
    public void CanParse_DouyinPlatform_ReturnsFalse()
    {
        var parser = new XiaohongshuParser();
        Assert.False(parser.CanParse(Platform.douyin, "https://v.douyin.com/abc"));
    }
}
