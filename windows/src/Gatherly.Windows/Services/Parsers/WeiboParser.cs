using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 微博解析器 — HTTP 主路径，对齐 macOS WeiboParser.swift
/// 优先使用 m.weibo.cn AJAX API，兜底 HTML render_data，再兜底桌面端 meta
/// 暂不实现 WebView2 降级
/// </summary>
public partial class WeiboParser : IContentParser
{
    private static readonly HttpClient DefaultHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1" }
        }
    };

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.weibo;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var statusId = UrlNormalizer.ExtractWeiboId(request.Url);
            if (string.IsNullOrEmpty(statusId))
                return ParseResult.Fail("无法从链接提取微博 ID");

            // 1. 移动端 AJAX API（对齐 macOS parseMobilePage）
            var mobileResult = await ParseViaMobileAsync(request.Url, statusId, cancellationToken);
            if (mobileResult != null)
                return mobileResult;

            // 2. 桌面端 meta 兜底（对齐 macOS parseDesktopPage）
            var desktopResult = await ParseViaDesktopAsync(statusId, cancellationToken);
            return desktopResult;
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
    /// 移动端 AJAX API（对齐 macOS parseMobilePage）
    /// </summary>
    private async Task<ParseResult?> ParseViaMobileAsync(string originalUrl, string statusId, CancellationToken ct)
    {
        // AJAX API（对齐 macOS：带 X-Requested-With 头绕过反爬）
        var apiUrl = $"https://m.weibo.cn/statuses/show?id={statusId}";
        using var apiRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        apiRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
        apiRequest.Headers.Add("Accept", "application/json, text/plain, */*");
        apiRequest.Headers.Add("Referer", $"https://m.weibo.cn/detail/{statusId}");

        var apiResponse = await DefaultHttpClient.SendAsync(apiRequest, ct);
        if (apiResponse.StatusCode != System.Net.HttpStatusCode.OK)
        {
            // 兜底：尝试 HTML render_data（对齐 macOS）
            var htmlResult = await ParseMobileHtmlAsync(originalUrl, statusId, ct);
            if (htmlResult != null) return htmlResult;
            return null;
        }

        var apiJson = await apiResponse.Content.ReadAsStringAsync(ct);
        var apiDoc = JsonDocument.Parse(apiJson);
        var apiRoot = apiDoc.RootElement;

        if (apiRoot.TryGetProperty("ok", out var ok) && ok.GetInt32() == 1 &&
            apiRoot.TryGetProperty("data", out var data))
        {
            return ParseWeiboJson(data, statusId, originalUrl);
        }

        // AJAX 失败，尝试 HTML
        var htmlFallback = await ParseMobileHtmlAsync(originalUrl, statusId, ct);
        return htmlFallback;
    }

    /// <summary>
    /// 移动端 HTML render_data 兜底（对齐 macOS）
    /// </summary>
    private async Task<ParseResult?> ParseMobileHtmlAsync(string originalUrl, string statusId, CancellationToken ct)
    {
        var mobileUrl = $"https://m.weibo.cn/detail/{statusId}";
        var html = await DefaultHttpClient.GetStringAsync(mobileUrl, ct);

        // 提取 var $render_data = [JSON]
        var renderMatch = RenderDataPattern().Match(html);
        if (!renderMatch.Success) return null;

        var renderStr = renderMatch.Groups[1].Value.Trim();
        var doc = JsonDocument.Parse(renderStr);
        var root = doc.RootElement;

        // render_data 是数组，取第一个元素的 data.status
        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            var first = root[0];
            if (first.TryGetProperty("data", out var data) &&
                data.TryGetProperty("status", out var status))
            {
                return ParseWeiboJson(status, statusId, originalUrl);
            }
        }

        return null;
    }

    /// <summary>
    /// 从微博 JSON 数据提取内容（共用逻辑，对齐 macOS parseWeiboJSON）
    /// </summary>
    private ParseResult ParseWeiboJson(JsonElement status, string statusId, string originalUrl)
    {
        // 正文：text 字段含 HTML 标签，需要清理
        var rawText = GetString(status, "text") ?? "";
        var text = StripHtml(rawText);

        // 图片列表（对齐 macOS pics → large.url 或 url + makeWeiboImageLarge）
        var imageUrls = new List<string>();
        if (status.TryGetProperty("pics", out var pics) && pics.ValueKind == JsonValueKind.Array)
        {
            foreach (var pic in pics.EnumerateArray())
            {
                string? url = null;
                if (pic.TryGetProperty("large", out var large) &&
                    large.TryGetProperty("url", out var largeUrl))
                {
                    url = largeUrl.GetString();
                }
                else if (pic.TryGetProperty("url", out var picUrl))
                {
                    url = MakeWeiboImageLarge(picUrl.GetString() ?? "");
                }

                if (!string.IsNullOrEmpty(url))
                    imageUrls.Add(url);
            }
        }

        // 作者
        string? author = null;
        string? authorId = null;
        if (status.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
        {
            author = GetString(user, "screen_name");
            if (user.TryGetProperty("id_str", out var idStr))
                authorId = idStr.GetString();
            else if (user.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var idVal))
                authorId = idVal.ToString();
        }

        // 发布时间
        DateTimeOffset? publishDate = null;
        var createdAt = GetString(status, "created_at");
        if (!string.IsNullOrEmpty(createdAt))
            publishDate = ParseWeiboDate(createdAt);

        // 标题：正文前 80 字（对齐 macOS）
        var title = string.IsNullOrEmpty(text) ? $"微博 {statusId}" : text.Length > 80 ? text[..80] : text;

        // 封面：首图，正文图片列表移除首图避免重复（对齐 macOS）
        var coverUrl = imageUrls.FirstOrDefault();
        var bodyImages = imageUrls.Count > 0 ? imageUrls.Skip(1).ToList() : new List<string>();

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = text,
            Author = author,
            AuthorId = authorId,
            PublishDate = publishDate,
            CoverUrl = coverUrl,
            ImageUrls = bodyImages,
            VideoUrl = null,
            PlatformContentId = statusId,
            Platform = Platform.weibo,
            OriginalUrl = originalUrl,
            NormalizedUrl = UrlNormalizer.Normalize(originalUrl, Platform.weibo)
        });
    }

    /// <summary>
    /// 桌面端 meta 兜底（对齐 macOS parseDesktopPage）
    /// </summary>
    private async Task<ParseResult> ParseViaDesktopAsync(string statusId, CancellationToken ct)
    {
        var desktopUrl = $"https://weibo.com/status/{statusId}";
        var html = await DefaultHttpClient.GetStringAsync(desktopUrl, ct);

        var title = ExtractMetaProperty(html, "og:title") ?? $"微博 {statusId}";
        var desc = ExtractMetaProperty(html, "og:description");
        var cover = ExtractMetaProperty(html, "og:image");
        var body = !string.IsNullOrEmpty(desc) ? StripHtml(desc) : null;

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = body,
            CoverUrl = cover,
            PlatformContentId = statusId,
            Platform = Platform.weibo,
            OriginalUrl = desktopUrl,
            NormalizedUrl = UrlNormalizer.Normalize(desktopUrl, Platform.weibo)
        });
    }

    #region Helpers

    /// <summary>
    /// 微博图片 URL 升级为高清（对齐 macOS makeWeiboImageLarge）
    /// </summary>
    private static string MakeWeiboImageLarge(string url)
    {
        foreach (var (old, replacement) in new[] { ("orj360", "large"), ("orj480", "large"), ("thumb", "large"), ("thumbnail", "large") })
            url = url.Replace(old, replacement);
        return url;
    }

    /// <summary>
    /// 清理 HTML 标签（对齐 macOS stripHTML）
    /// </summary>
    public static string StripHtml(string html)
    {
        var result = BrPattern().Replace(html, "\n");
        result = HtmlTagPattern().Replace(result, "");
        result = result.Replace("&amp;", "&");
        result = result.Replace("&lt;", "<");
        result = result.Replace("&gt;", ">");
        result = result.Replace("&nbsp;", " ");
        result = result.Replace("&quot;", "\"");
        result = result.Replace("&#39;", "'");
        result = WhitespacePattern().Replace(result, " ");
        return result.Trim();
    }

    private static DateTimeOffset? ParseWeiboDate(string dateStr)
    {
        if (DateTimeOffset.TryParseExact(dateStr, "ddd MMM dd HH:mm:ss zzz yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static string? ExtractMetaProperty(string html, string property)
    {
        var match = MetaPattern(property).Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    #endregion

    #region Regex Patterns

    [GeneratedRegex("""var \$render_data\s*=\s*(\[.*?\])\s*\[0\]""", RegexOptions.Singleline)]
    private static partial Regex RenderDataPattern();

    [GeneratedRegex("""<br\s*/?>""", RegexOptions.IgnoreCase)]
    private static partial Regex BrPattern();

    [GeneratedRegex("""<[^>]+>""")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex("""\s+""")]
    private static partial Regex WhitespacePattern();

    private static Regex MetaPattern(string property)
        => new($"""{property}"\s+content="([^"]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    #endregion
}
