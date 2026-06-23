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

            // 尝试从 SSR 数据中提取 JSON（与 macOS 一致的优先级）
            var ssrResult = ExtractFromSsrData(html, request);
            if (ssrResult != null)
                return ssrResult;

            // 回退：从 meta 标签提取基础信息（与 macOS extractFromMetaTags 一致）
            return ExtractFromMetaTags(html, request);
        }
        catch (HttpRequestException ex)
        {
            return ParseResult.Fail($"HTTP 请求失败：{ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return ParseResult.Fail("请求超时");
        }
        catch (Exception ex)
        {
            return ParseResult.Fail($"解析失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 从 SSR 数据提取内容（对应 macOS extractFromSSRData）
    /// 优先级：window._ROUTER_DATA（移动端）→ RENDER_DATA（桌面端）
    /// </summary>
    private ParseResult? ExtractFromSsrData(string html, ParseRequest request)
    {
        // 移动端页面使用 window._ROUTER_DATA
        var routerMatch = RouterDataPattern().Match(html);
        if (routerMatch.Success)
        {
            var jsonStr = routerMatch.Groups[1].Value.Trim();
            if (jsonStr.EndsWith(';'))
                jsonStr = jsonStr[..^1];

            try
            {
                var json = JsonDocument.Parse(jsonStr);
                var mobileResult = ParseMobileJson(json.RootElement, request);
                if (mobileResult != null)
                    return mobileResult;
            }
            catch
            {
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
            videoInfoRes.TryGetProperty("item_list", out var itemList) &&
            itemList.GetArrayLength() > 0)
        {
            return ParseNoteDetail(itemList[0], request);
        }

        return null;
    }

    /// <summary>
    /// 解析单条笔记详情（对应 macOS parseNoteDetail）
    /// </summary>
    private ParseResult? ParseNoteDetail(JsonElement detail, ParseRequest request)
    {
        var desc = detail.TryGetProperty("desc", out var descEl) ? descEl.GetString() : null;

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
                playUrls.GetArrayLength() > 0)
            {
                var url = playUrls[0].GetString() ?? "";
                // 去水印：/playwm/ → /play/（与 macOS 一致）
                videoUrl = url.Replace("/playwm/", "/play/");
            }
        }

        // 提取图片列表（与 macOS 一致）
        var imageUrls = new List<string>();
        if (detail.TryGetProperty("images", out var images))
        {
            foreach (var image in images.EnumerateArray())
            {
                if (image.TryGetProperty("url_list", out var urlList) &&
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
            PlatformContentId = UrlNormalizer.ExtractContentId(request.Url, Platform.douyin),
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
            cover.GetArrayLength() > 0)
        {
            var firstCover = cover[0];
            if (firstCover.TryGetProperty("urlList", out var urlList) &&
                urlList.GetArrayLength() > 0)
            {
                coverUrl = urlList[0].GetString();
            }
        }

        // 图片列表
        var imageUrls = new List<string>();
        if (detailEl.TryGetProperty("images", out var images))
        {
            foreach (var image in images.EnumerateArray())
            {
                if (image.TryGetProperty("url_list", out var urlList) &&
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
            PlatformContentId = UrlNormalizer.ExtractContentId(request.Url, Platform.douyin),
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
            PlatformContentId = UrlNormalizer.ExtractContentId(request.Url, Platform.douyin),
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
