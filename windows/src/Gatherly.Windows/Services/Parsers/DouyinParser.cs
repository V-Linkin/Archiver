using System.Text.Json;
using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 抖音解析器 — 对齐 macOS DouyinParser.swift
/// 使用移动端 UA 获取页面，从 window._ROUTER_DATA 提取 SSR 数据
/// 支持视频笔记（aweme_type=0）和图文笔记（aweme_type=2）
/// </summary>
public partial class DouyinParser : IContentParser
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1" },
            { "Referer", "https://www.douyin.com/" }
        }
    };

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.douyin;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await SharedHttpClient.GetStringAsync(request.Url, cancellationToken);

            // 诊断日志
            var log = $"[{DateTime.Now:HH:mm:ss}] URL={request.Url}\nHTML length={html.Length}\nHas _ROUTER_DATA={html.Contains("window._ROUTER_DATA")}\n";

            // 尝试从 SSR 数据中提取 JSON（与 macOS 一致的优先级）
            var ssrResult = ExtractFromSsrData(html, request);
            if (ssrResult != null)
            {
                log += $"SSR result: Status={ssrResult.Status}, Title={ssrResult.Content?.Title}, Author={ssrResult.Content?.Author}\n";
                WriteLog(log);
                return ssrResult;
            }

            log += "SSR result: null (fell through to meta)\n";

            // 回退：从 meta 标签提取基础信息（与 macOS extractFromMetaTags 一致）
            var metaResult = ExtractFromMetaTags(html, request);
            log += $"Meta result: Status={metaResult.Status}, Title={metaResult.Content?.Title}, Author={metaResult.Content?.Author}\n";
            WriteLog(log);
            return metaResult;
        }
        catch (HttpRequestException ex)
        {
            WriteLog($"[{DateTime.Now:HH:mm:ss}] HTTP error: {ex.Message}\n");
            return ParseResult.Fail($"HTTP 请求失败：{ex.Message}");
        }
        catch (TaskCanceledException)
        {
            WriteLog($"[{DateTime.Now:HH:mm:ss}] Timeout\n");
            return ParseResult.Fail("请求超时");
        }
        catch (Exception ex)
        {
            WriteLog($"[{DateTime.Now:HH:mm:ss}] Parse error: {ex.Message}\n{ex.StackTrace}\n");
            return ParseResult.Fail($"解析失败：{ex.Message}");
        }
    }

    private static void WriteLog(string message)
    {
        try
        {
            var logPath = Path.Combine(Path.GetTempPath(), "douyin_parser_debug.log");
            File.AppendAllText(logPath, message + "\n");
        }
        catch { }
    }

    /// <summary>
    /// 从 SSR 数据提取内容（对应 macOS extractFromSSRData）
    /// 优先级：window._ROUTER_DATA（移动端）→ RENDER_DATA（桌面端）
    /// </summary>
    private ParseResult? ExtractFromSsrData(string html, ParseRequest request)
    {
        var log = "";

        // 移动端页面使用 window._ROUTER_DATA
        var routerMatch = RouterDataPattern().Match(html);
        log += $"Regex match: {routerMatch.Success}\n";

        if (routerMatch.Success)
        {
            var jsonStr = routerMatch.Groups[1].Value.Trim();
            if (jsonStr.EndsWith(';'))
                jsonStr = jsonStr[..^1];

            log += $"JSON length: {jsonStr.Length}\n";

            try
            {
                var json = JsonDocument.Parse(jsonStr);
                log += "JSON parsed OK\n";

                // 列出 loaderData 键
                if (json.RootElement.TryGetProperty("loaderData", out var ld))
                {
                    log += "loaderData keys: ";
                    foreach (var p in ld.EnumerateObject())
                        log += $"{p.Name}({p.Value.ValueKind}) ";
                    log += "\n";
                }

                var mobileResult = ParseMobileJson(json.RootElement, request);
                log += $"ParseMobileJson result: {(mobileResult != null ? "SUCCESS" : "NULL")}\n";
                if (mobileResult?.Content != null)
                    log += $"  Title=[{mobileResult.Content.Title}] Author=[{mobileResult.Content.Author}] Cover=[{mobileResult.Content.CoverUrl}] Video=[{mobileResult.Content.VideoUrl}] Images={mobileResult.Content.ImageUrls.Count} Pcid=[{mobileResult.Content.PlatformContentId}]\n";

                WriteLog(log);
                if (mobileResult != null)
                    return mobileResult;
            }
            catch (Exception ex)
            {
                log += $"JSON parse error: {ex.Message}\n";
                WriteLog(log);
                // JSON 解析失败，继续尝试其他方式
            }
        }

        // 桌面端：尝试 RENDER_DATA
        var renderMatch = RenderDataPattern().Match(html);
        if (renderMatch.Success)
        {
            var jsonStr = Uri.UnescapeDataString(renderMatch.Groups[1].Value);
            try
            {
                var json = JsonDocument.Parse(jsonStr);
                var desktopResult = ParseDesktopJson(json.RootElement, request);
                if (desktopResult != null)
                    return desktopResult;
            }
            catch
            {
                // JSON 解析失败
            }
        }

        return null;
    }

    /// <summary>
    /// 解析移动端 JSON（对应 macOS parseMobileJSON + parseNoteDetail）
    /// 路径：loaderData → note_(id)/page 或 video_(id)/page → aweme.detail / videoInfoRes.item_list
    /// </summary>
    private ParseResult? ParseMobileJson(JsonElement root, ParseRequest request)
    {
        if (!root.TryGetProperty("loaderData", out var loaderData))
            return null;

        // 找到 note_(id)/page 或 video_(id)/page 键
        JsonElement? page = null;
        foreach (var prop in loaderData.EnumerateObject())
        {
            if ((prop.Name.StartsWith("note_(") || prop.Name.StartsWith("video_("))
                && prop.Name.EndsWith(")/page"))
            {
                page = prop.Value;
                break;
            }
        }

        if (page == null)
            return null;

        // 尝试 aweme.detail
        if (page.Value.TryGetProperty("aweme", out var aweme) &&
            aweme.TryGetProperty("detail", out var detail))
        {
            return ParseNoteDetail(detail, request);
        }

        // 尝试 videoInfoRes.item_list
        if (page.Value.TryGetProperty("videoInfoRes", out var videoInfoRes) &&
            videoInfoRes.ValueKind == System.Text.Json.JsonValueKind.Object &&
            videoInfoRes.TryGetProperty("item_list", out var itemList) &&
            itemList.ValueKind == System.Text.Json.JsonValueKind.Array &&
            itemList.GetArrayLength() > 0)
        {
            return ParseNoteDetail(itemList[0], request);
        }

        // 如果 videoInfoRes 存在但 item_list 为 Null，尝试从 videoInfoRes 顶层提取数据
        if (page.Value.TryGetProperty("videoInfoRes", out var viRes2) &&
            viRes2.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            // 尝试直接从 videoInfoRes 提取 desc / author / video / images
            var desc = GetString(viRes2, "desc");
            string? author = null;
            if (viRes2.TryGetProperty("author", out var authorEl) && authorEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                author = GetString(authorEl, "nickname");

            string? videoUrl = null;
            if (viRes2.TryGetProperty("video", out var videoEl) && videoEl.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (videoEl.TryGetProperty("play_addr", out var playAddr) && playAddr.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    playAddr.TryGetProperty("url_list", out var urls) && urls.ValueKind == System.Text.Json.JsonValueKind.Array && urls.GetArrayLength() > 0)
                {
                    videoUrl = urls[0].GetString()?.Replace("/playwm/", "/play/");
                }
            }

            string? coverUrl = null;
            if (viRes2.TryGetProperty("video", out var cvEl) && cvEl.ValueKind == System.Text.Json.JsonValueKind.Object &&
                cvEl.TryGetProperty("cover", out var coverEl) && coverEl.ValueKind == System.Text.Json.JsonValueKind.Object &&
                coverEl.TryGetProperty("url_list", out var coverUrls) && coverUrls.ValueKind == System.Text.Json.JsonValueKind.Array && coverUrls.GetArrayLength() > 0)
            {
                coverUrl = coverUrls[0].GetString();
            }

            if (desc != null || author != null || videoUrl != null || coverUrl != null)
            {
                var title = !string.IsNullOrEmpty(desc) ? desc.Length > 50 ? desc[..50] : desc : "";
                return ParseResult.Success(new ParsedContent
                {
                    Title = title,
                    Body = desc,
                    Author = author,
                    CoverUrl = coverUrl,
                    VideoUrl = videoUrl,
                    PlatformContentId = UrlNormalizer.ExtractContentId(request.Url, Platform.douyin),
                    Platform = Platform.douyin,
                    OriginalUrl = request.Url,
                    NormalizedUrl = request.NormalizedUrl
                });
            }
        }

        return null;
    }

    /// <summary>
    /// 解析单条笔记详情（对应 macOS parseNoteDetail）
    /// </summary>
    private ParseResult? ParseNoteDetail(JsonElement detail, ParseRequest request)
    {
        var desc = detail.TryGetProperty("desc", out var descEl) ? descEl.GetString() : null;

        // 提取 aweme_id 作为 PlatformContentId（从 JSON 中获取，不依赖 URL 正则）
        var awemeId = GetString(detail, "aweme_id");

        // 提取作者
        string? author = null;
        string? authorId = null;
        if (detail.TryGetProperty("author", out var authorInfo))
        {
            author = GetString(authorInfo, "nickname");
            authorId = GetString(authorInfo, "uid");
        }

        // aweme_type: 0=视频, 2=图文（对应 macOS 逻辑）
        var awemeType = detail.TryGetProperty("aweme_type", out var typeEl) && typeEl.TryGetInt32(out var t) ? t : 0;
        var isImageNote = awemeType == 2;

        // 提取封面（仅视频笔记使用，与 macOS 一致）
        string? coverUrl = null;
        if (!isImageNote && detail.TryGetProperty("video", out var video))
        {
            if (video.TryGetProperty("cover", out var cover) &&
                cover.TryGetProperty("url_list", out var coverUrls) &&
                coverUrls.ValueKind == System.Text.Json.JsonValueKind.Array &&
                coverUrls.GetArrayLength() > 0)
            {
                coverUrl = coverUrls[0].GetString();
            }
        }

        // 提取视频 URL（仅视频笔记，与 macOS 一致）
        string? videoUrl = null;
        if (!isImageNote && detail.TryGetProperty("video", out var videoForPlay))
        {
            if (videoForPlay.TryGetProperty("play_addr", out var playAddr) &&
                playAddr.TryGetProperty("url_list", out var playUrls) &&
                playUrls.ValueKind == System.Text.Json.JsonValueKind.Array &&
                playUrls.GetArrayLength() > 0)
            {
                var url = playUrls[0].GetString() ?? "";
                // 去水印：/playwm/ → /play/（与 macOS 一致）
                videoUrl = url.Replace("/playwm/", "/play/");
            }
        }

        // 提取图片列表（与 macOS 一致）
        var imageUrls = new List<string>();
            if (detail.TryGetProperty("images", out var images) &&
                images.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var image in images.EnumerateArray())
                {
                    if (image.TryGetProperty("url_list", out var urlList) &&
                        urlList.ValueKind == System.Text.Json.JsonValueKind.Array &&
                        urlList.GetArrayLength() > 0)
                {
                    var firstUrl = urlList[0].GetString();
                    if (firstUrl != null)
                        imageUrls.Add(firstUrl);
                }
            }
        }

        // 封面去重：如果封面和首图相同，移除首图（与 macOS 一致）
        if (coverUrl != null && imageUrls.Count > 0 && imageUrls[0] == coverUrl)
        {
            imageUrls.RemoveAt(0);
        }

        // 标题：优先用 title，没有则用 desc 去掉 #话题 后截取前50字（与 macOS 一致）
        string title;
        if (detail.TryGetProperty("title", out var titleEl) && !string.IsNullOrWhiteSpace(titleEl.GetString()))
        {
            title = titleEl.GetString()!;
        }
        else if (desc != null)
        {
            // 去掉 #话题 格式（与 macOS pattern "#[^#\n\t ]+" 一致）
            var cleaned = HashtagPattern().Replace(desc, "").Trim();
            title = cleaned.Length > 50 ? cleaned[..50] : cleaned;
        }
        else
        {
            title = "";
        }

        if (string.IsNullOrEmpty(title) && desc == null)
            return null;

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = desc,
            Author = author,
            AuthorId = authorId,
            CoverUrl = coverUrl,
            ImageUrls = imageUrls,
            VideoUrl = videoUrl,
            PlatformContentId = awemeId ?? UrlNormalizer.ExtractContentId(request.Url, Platform.douyin) ?? "",
            Platform = Platform.douyin,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    /// <summary>
    /// 解析桌面端 JSON（对应 macOS parseDesktopJSON）
    /// 递归查找 "detail" 键
    /// </summary>
    private ParseResult? ParseDesktopJson(JsonElement root, ParseRequest request)
    {
        var detail = FindValueByKey(root, "detail");
        if (detail == null)
            return null;

        var detailEl = detail.Value;

        var title = GetString(detailEl, "title");
        var desc = GetString(detailEl, "desc");

        string? author = null;
        if (detailEl.TryGetProperty("authorInfo", out var authorInfo))
        {
            author = GetString(authorInfo, "nickname");
        }

        // 视频 URL（桌面端路径：video.playAddr[0].src）
        string? videoUrl = null;
        if (detailEl.TryGetProperty("video", out var video))
        {
            if (video.TryGetProperty("playAddr", out var playAddr) &&
                playAddr.ValueKind == System.Text.Json.JsonValueKind.Array &&
                playAddr.GetArrayLength() > 0)
            {
                var first = playAddr[0];
                if (first.TryGetProperty("src", out var srcEl))
                {
                    var src = srcEl.GetString() ?? "";
                    videoUrl = src.Replace("/playwm/", "/play/");
                }
            }
        }

        // 封面（桌面端路径：video.cover[0].urlList）
        string? coverUrl = null;
        if (detailEl.TryGetProperty("video", out var videoForCover) &&
            videoForCover.TryGetProperty("cover", out var cover) &&
            cover.ValueKind == System.Text.Json.JsonValueKind.Array &&
            cover.GetArrayLength() > 0)
        {
            var firstCover = cover[0];
            if (firstCover.TryGetProperty("urlList", out var urlList) &&
                urlList.ValueKind == System.Text.Json.JsonValueKind.Array &&
                urlList.GetArrayLength() > 0)
            {
                coverUrl = urlList[0].GetString();
            }
        }

        // 图片列表
        var imageUrls = new List<string>();
        if (detailEl.TryGetProperty("images", out var images) &&
            images.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var image in images.EnumerateArray())
            {
                if (image.TryGetProperty("url_list", out var urlList) &&
                    urlList.ValueKind == System.Text.Json.JsonValueKind.Array &&
                    urlList.GetArrayLength() > 0)
                {
                    var firstUrl = urlList[0].GetString();
                    if (firstUrl != null)
                        imageUrls.Add(firstUrl);
                }
            }
        }

        if (title == null && desc == null)
            return null;

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = desc,
            Author = author,
            CoverUrl = coverUrl,
            ImageUrls = imageUrls,
            VideoUrl = videoUrl,
            PlatformContentId = UrlNormalizer.ExtractContentId(request.Url, Platform.douyin) ?? "",
            Platform = Platform.douyin,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    /// <summary>
    /// 从 meta 标签提取基础信息（对应 macOS extractFromMetaTags）
    /// </summary>
    private ParseResult ExtractFromMetaTags(string html, ParseRequest request)
    {
        var title = ExtractMetaContent(html, "og:title")
            ?? ExtractMetaContent(html, "twitter:title");
        var desc = ExtractMetaContent(html, "og:description")
            ?? ExtractMetaByName(html, "description");
        var cover = ExtractMetaContent(html, "og:image")
            ?? ExtractMetaContent(html, "twitter:image");

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = desc,
            CoverUrl = cover,
            PlatformContentId = UrlNormalizer.ExtractContentId(request.Url, Platform.douyin) ?? "",
            Platform = Platform.douyin,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    #region Helpers

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    /// <summary>
    /// 递归查找 JSON 中指定键的值（对应 macOS findValue(in:key:)）
    /// </summary>
    private static JsonElement? FindValueByKey(JsonElement obj, string key)
    {
        if (obj.TryGetProperty(key, out var value))
            return value;

        if (obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in obj.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    var found = FindValueByKey(prop.Value, key);
                    if (found != null)
                        return found;
                }
            }
        }

        return null;
    }

    private static string? ExtractMetaContent(string html, string property)
    {
        var match = MetaPropertyPattern(property).Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractMetaByName(string html, string name)
    {
        var match = MetaNamePattern(name).Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    #endregion

    #region Regex Patterns

    /// <summary>
    /// 匹配 window._ROUTER_DATA = { JSON }
    /// </summary>
    [GeneratedRegex("""window\._ROUTER_DATA\s*=\s*(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex RouterDataPattern();

    /// <summary>
    /// 测试用：暴露 RouterDataPattern 供单元测试使用
    /// </summary>
    public static Match RouterDataPatternForTest(string html) => RouterDataPattern().Match(html);

    /// <summary>
    /// 匹配 &lt;script id="RENDER_DATA" type="application/json"&gt;{ URL-encoded JSON }&lt;/script&gt;
    /// </summary>
    [GeneratedRegex("""<script id="RENDER_DATA" type="application/json">(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex RenderDataPattern();

    /// <summary>
    /// 匹配 #话题 标签
    /// </summary>
    [GeneratedRegex("""#[^#\n\t ]+""")]
    private static partial Regex HashtagPattern();

    /// <summary>
    /// 匹配 meta property 标签
    /// </summary>
    private static Regex MetaPropertyPattern(string property)
    {
        return new($"""<meta[^>]+property="{property}"[^>]+content="([^"]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    /// <summary>
    /// 匹配 meta name 标签
    /// </summary>
    private static Regex MetaNamePattern(string name)
    {
        return new($"""<meta[^>]+name="{name}"[^>]+content="([^"]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    #endregion
}
