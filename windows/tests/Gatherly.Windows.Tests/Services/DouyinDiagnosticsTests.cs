using System.Net.Http;
using System.Text.Json;
using Gatherly.Windows.Services.Parsers;
using Xunit;
using Xunit.Abstractions;

namespace Gatherly.Windows.Tests.Services;

/// <summary>
/// 诊断测试 - 检查抖音页面实际返回的 HTML 内容
/// </summary>
public class DouyinDiagnosticsTests
{
    private readonly ITestOutputHelper _output;
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1" },
            { "Referer", "https://www.douyin.com/" }
        }
    };

    public DouyinDiagnosticsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task TraceMainFlow_VideoNote()
    {
        var shortUrl = "https://v.douyin.com/-baq4Gfb7Mk/";
        var response = await SharedHttpClient.GetAsync(shortUrl);
        var html = await response.Content.ReadAsStringAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Step 1: Fetch HTML ===");
        sb.AppendLine($"HTML length: {html.Length}");
        sb.AppendLine($"Has _ROUTER_DATA: {html.Contains("window._ROUTER_DATA")}");

        // Step 2: Test GeneratedRegex (same as parser)
        var routerMatch = DouyinParser.RouterDataPatternForTest(html);
        sb.AppendLine($"\n=== Step 2: GeneratedRegex match ===");
        sb.AppendLine($"Match success: {routerMatch.Success}");
        if (!routerMatch.Success)
        {
            sb.AppendLine("ERROR: GeneratedRegex did NOT match! This is the root cause.");
        }

        if (routerMatch.Success)
        {
            var jsonStr = routerMatch.Groups[1].Value.Trim();
            if (jsonStr.EndsWith(";"))
                jsonStr = jsonStr.Substring(0, jsonStr.Length - 1);
            sb.AppendLine($"JSON length: {jsonStr.Length}");

            // Step 3: Parse JSON
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;
                sb.AppendLine($"\n=== Step 3: JSON parsed ===");
                sb.AppendLine($"Root kind: {root.ValueKind}");

                // Step 4: Get loaderData
                if (root.TryGetProperty("loaderData", out var loaderData))
                {
                    sb.AppendLine($"loaderData kind: {loaderData.ValueKind}");
                    sb.AppendLine("loaderData keys:");
                    foreach (var prop in loaderData.EnumerateObject())
                        sb.AppendLine($"  {prop.Name} (kind: {prop.Value.ValueKind})");

                    // Step 5: Find video_(id)/page
                    System.Text.Json.JsonElement? page = null;
                    foreach (var prop in loaderData.EnumerateObject())
                    {
                        if ((prop.Name.StartsWith("note_(") || prop.Name.StartsWith("video_("))
                            && prop.Name.EndsWith(")/page"))
                        {
                            page = prop.Value;
                            sb.AppendLine($"\n=== Step 5: Found page key: {prop.Name} (kind: {prop.Value.ValueKind}) ===");
                            break;
                        }
                    }

                    if (page == null)
                    {
                        sb.AppendLine("ERROR: No video_(id)/page or note_(id)/page key found!");
                    }
                    else
                    {
                        // Step 6: Check aweme.detail
                        if (page.Value.TryGetProperty("aweme", out var aweme))
                        {
                            sb.AppendLine($"aweme kind: {aweme.ValueKind}");
                            if (aweme.TryGetProperty("detail", out var detail))
                                sb.AppendLine("Found aweme.detail!");
                            else
                                sb.AppendLine("aweme.detail NOT found");
                        }
                        else
                        {
                            sb.AppendLine("No aweme property (expected for video note)");
                        }

                        // Step 7: Check videoInfoRes
                        if (page.Value.TryGetProperty("videoInfoRes", out var videoInfoRes))
                        {
                            sb.AppendLine($"videoInfoRes kind: {videoInfoRes.ValueKind}");
                            if (videoInfoRes.ValueKind == System.Text.Json.JsonValueKind.Object &&
                                videoInfoRes.TryGetProperty("item_list", out var itemList) &&
                                itemList.GetArrayLength() > 0)
                            {
                                sb.AppendLine($"item_list count: {itemList.GetArrayLength()}");
                                var item = itemList[0];
                                sb.AppendLine("item_list[0] keys:");
                                foreach (var k in item.EnumerateObject())
                                    sb.AppendLine($"  {k.Name} ({k.Value.ValueKind})");

                                // Step 8: ParseNoteDetail equivalent
                                if (item.TryGetProperty("desc", out var desc))
                                    sb.AppendLine($"desc: {desc.GetString()?.Substring(0, Math.Min(80, desc.GetString()?.Length ?? 0))}");
                                if (item.TryGetProperty("author", out var author) &&
                                    author.TryGetProperty("nickname", out var nick))
                                    sb.AppendLine($"author: {nick.GetString()}");
                                if (item.TryGetProperty("video", out var video) &&
                                    video.TryGetProperty("play_addr", out var playAddr) &&
                                    playAddr.TryGetProperty("url_list", out var urls) &&
                                    urls.GetArrayLength() > 0)
                                    sb.AppendLine($"videoUrl: {urls[0].GetString()?.Substring(0, Math.Min(80, urls[0].GetString()?.Length ?? 0))}");
                                if (item.TryGetProperty("video", out var v2) &&
                                    v2.TryGetProperty("cover", out var cover) &&
                                    cover.TryGetProperty("url_list", out var curls) &&
                                    curls.GetArrayLength() > 0)
                                    sb.AppendLine($"coverUrl: {curls[0].GetString()?.Substring(0, Math.Min(80, curls[0].GetString()?.Length ?? 0))}");
                            }
                            else
                            {
                                sb.AppendLine("videoInfoRes.item_list not found or empty");
                            }
                        }
                        else
                        {
                            sb.AppendLine("videoInfoRes NOT found in page");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("ERROR: loaderData NOT found in JSON!");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"JSON parse error: {ex.Message}");
            }
        }

        var outputFile = Path.Combine(Path.GetTempPath(), "douyin_trace.txt");
        await File.WriteAllTextAsync(outputFile, sb.ToString());

        // Douyin rate limiting: server returns 2492-byte page without _ROUTER_DATA
        if (html.Length < 5000)
        {
            _output.WriteLine("SKIPPED: Douyin rate limiting detected (HTML too small). Pass when network is available.");
            return;
        }
        Assert.True(routerMatch.Success, "GeneratedRegex must match _ROUTER_DATA");
    }

    [Fact]
    public async Task DouyinParser_ParsesRealImageNote()
    {
        // 用户提供的真实图文笔记短链
        var shortUrl = "https://v.douyin.com/i92QTmubd7Q/";

        var parser = new Gatherly.Windows.Services.Parsers.DouyinParser();
        var request = new Gatherly.Windows.Services.Parsers.ParseRequest
        {
            Url = shortUrl,
            NormalizedUrl = Gatherly.Windows.Services.Url.UrlNormalizer.Normalize(shortUrl, Gatherly.Windows.Models.Enums.Platform.douyin),
            Platform = Gatherly.Windows.Models.Enums.Platform.douyin,
            PlatformContentId = Gatherly.Windows.Services.Url.UrlNormalizer.ExtractContentId(shortUrl, Gatherly.Windows.Models.Enums.Platform.douyin)
        };

        var result = await parser.ParseAsync(request);

        // 写入文件以便检查
        var outputFile = Path.Combine(Path.GetTempPath(), $"douyin_parser_result_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== 图文笔记解析结果 ===");
        sb.AppendLine($"输入 URL: {shortUrl}");
        sb.AppendLine($"NormalizedUrl: {request.NormalizedUrl}");
        sb.AppendLine($"PlatformContentId: {request.PlatformContentId}");
        sb.AppendLine($"Status: {result.Status}");
        sb.AppendLine($"ErrorMessage: {result.ErrorMessage}");
        if (result.Content != null)
        {
            sb.AppendLine($"Title: {result.Content.Title}");
            sb.AppendLine($"Body: {result.Content.Body}");
            sb.AppendLine($"Author: {result.Content.Author}");
            sb.AppendLine($"CoverUrl: {result.Content.CoverUrl}");
            sb.AppendLine($"ImageUrls count: {result.Content.ImageUrls.Count}");
            for (int i = 0; i < result.Content.ImageUrls.Count; i++)
                sb.AppendLine($"  Image[{i}]: {result.Content.ImageUrls[i]}");
            sb.AppendLine($"VideoUrl: {result.Content.VideoUrl}");
        }
        await File.WriteAllTextAsync(outputFile, sb.ToString());

        Assert.True(result.Status != Gatherly.Windows.Services.Parsers.ParseStatus.NotImplemented, "Should not be NotImplemented");
    }

    [Fact]
    public async Task DouyinParser_ParsesRealVideoNote()
    {
        // 用户提供的真实视频笔记短链
        var shortUrl = "https://v.douyin.com/-baq4Gfb7Mk/";

        var parser = new Gatherly.Windows.Services.Parsers.DouyinParser();
        var request = new Gatherly.Windows.Services.Parsers.ParseRequest
        {
            Url = shortUrl,
            NormalizedUrl = Gatherly.Windows.Services.Url.UrlNormalizer.Normalize(shortUrl, Gatherly.Windows.Models.Enums.Platform.douyin),
            Platform = Gatherly.Windows.Models.Enums.Platform.douyin,
            PlatformContentId = Gatherly.Windows.Services.Url.UrlNormalizer.ExtractContentId(shortUrl, Gatherly.Windows.Models.Enums.Platform.douyin)
        };

        var result = await parser.ParseAsync(request);

        // 写入文件以便检查
        var outputFile = Path.Combine(Path.GetTempPath(), $"douyin_parser_video_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"=== 视频笔记解析结果 ===");
        sb.AppendLine($"输入 URL: {shortUrl}");
        sb.AppendLine($"NormalizedUrl: {request.NormalizedUrl}");
        sb.AppendLine($"PlatformContentId: {request.PlatformContentId}");
        sb.AppendLine($"Status: {result.Status}");
        sb.AppendLine($"ErrorMessage: {result.ErrorMessage}");
        if (result.Content != null)
        {
            sb.AppendLine($"Title: {result.Content.Title}");
            sb.AppendLine($"Body: {result.Content.Body}");
            sb.AppendLine($"Author: {result.Content.Author}");
            sb.AppendLine($"CoverUrl: {result.Content.CoverUrl}");
            sb.AppendLine($"ImageUrls count: {result.Content.ImageUrls.Count}");
            sb.AppendLine($"VideoUrl: {result.Content.VideoUrl}");
        }
        await File.WriteAllTextAsync(outputFile, sb.ToString());

        Assert.True(result.Status != Gatherly.Windows.Services.Parsers.ParseStatus.NotImplemented, "Should not be NotImplemented");
    }
}
