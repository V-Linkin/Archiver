using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 酷安解析器 — HTTP 主路径，对齐 macOS CoolapkParser.swift
/// 优先使用镜像站 coolapk1s.com 绕过原站反爬
/// 暂不实现 WebView2 降级
/// </summary>
public partial class CoolapkParser : IContentParser
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
            { "Referer", "https://www.coolapk.com/" }
        }
    };

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.coolapk;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 优先镜像站 coolapk1s.com（对齐 macOS）
            var mirrorResult = await ParseViaMirrorAsync(request, cancellationToken);
            if (mirrorResult != null)
                return mirrorResult;

            // 2. 原站 HTTP 兜底
            var httpResult = await ParseViaHttpAsync(request, cancellationToken);
            if (httpResult != null)
                return httpResult;

            return ParseResult.Fail("页面解析失败（镜像站和原站均无有效数据）");
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
    /// 镜像站解析（对齐 macOS parseViaMirror）
    /// </summary>
    private async Task<ParseResult?> ParseViaMirrorAsync(ParseRequest request, CancellationToken ct)
    {
        var mirrorUrl = ConvertToMirrorUrl(request.Url);
        if (mirrorUrl == null) return null;

        var html = await SharedHttpClient.GetStringAsync(mirrorUrl, ct);

        // 提取 __NEXT_DATA__ JSON（对齐 macOS extractFromNextData）
        var nextDataMatch = NextDataPattern().Match(html);
        if (!nextDataMatch.Success) return null;

        var jsonStr = nextDataMatch.Groups[1].Value;
        var doc = JsonDocument.Parse(jsonStr);
        var root = doc.RootElement;

        // JSON 路径：props.pageProps.feed（对齐 macOS）
        if (!root.TryGetProperty("props", out var props) ||
            !props.TryGetProperty("pageProps", out var pageProps) ||
            !pageProps.TryGetProperty("feed", out var feed))
            return null;

        return ParseFeedFromJson(feed, request.Url);
    }

    /// <summary>
    /// 原站 HTTP 解析（对齐 macOS parseViaHTTP）
    /// </summary>
    private async Task<ParseResult?> ParseViaHttpAsync(ParseRequest request, CancellationToken ct)
    {
        var html = await SharedHttpClient.GetStringAsync(request.Url, ct);

        // 尝试 __INITIAL_STATE__（对齐 macOS extractFromSSRData）
        var ssrMatch = InitialStatePattern().Match(html);
        if (ssrMatch.Success)
        {
            var jsonStr = ssrMatch.Groups[1].Value.Trim().TrimEnd(';');
            jsonStr = jsonStr.Replace("undefined", "null");
            var doc = JsonDocument.Parse(jsonStr);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data))
            {
                var title = GetString(data, "title");
                var desc = GetString(data, "description") ?? GetString(data, "content");
                var author = GetString(data, "username") ?? GetString(data, "author");

                var imageUrls = new List<string>();
                if (data.TryGetProperty("picArr", out var picArr) && picArr.ValueKind == JsonValueKind.Array)
                    foreach (var img in picArr.EnumerateArray())
                        if (img.ValueKind == JsonValueKind.String)
                            imageUrls.Add(img.GetString()!);

                var coverUrl = GetString(data, "message_cover");

                if (title != null || desc != null)
                {
                    if (string.IsNullOrEmpty(coverUrl) && imageUrls.Count > 0)
                        coverUrl = imageUrls[0];

                    return BuildResult(title, desc, author, coverUrl, imageUrls, request);
                }
            }
        }

        // 尝试 meta 标签（对齐 macOS extractFromMetaTags）
        var metaResult = ExtractFromMetaTags(html, request);
        if (metaResult != null && IsHighQualityContent(metaResult))
            return metaResult;

        return null;
    }

    /// <summary>
    /// 从 feed JSON 构建结果（对齐 macOS extractFromNextData）
    /// </summary>
    private ParseResult? ParseFeedFromJson(JsonElement feed, string originalUrl)
    {
        var title = GetString(feed, "title");
        var username = GetString(feed, "username");
        var message = GetString(feed, "message");

        var picArr = new List<string>();
        if (feed.TryGetProperty("picArr", out var picArrEl) && picArrEl.ValueKind == JsonValueKind.Array)
            foreach (var img in picArrEl.EnumerateArray())
                if (img.ValueKind == JsonValueKind.String)
                    picArr.Add(img.GetString()!);

        var messageCover = GetString(feed, "message_cover");

        // 封面：优先 message_cover，否则首图（对齐 macOS）
        var coverUrl = !string.IsNullOrEmpty(messageCover) ? messageCover : picArr.FirstOrDefault();

        // 图片转换为代理 URL（对齐 macOS convertToProxyURL，绕过防盗链）
        var imageUrls = picArr.Select(ConvertToProxyUrl).Where(u => u != null).ToList()!;

        // 首图去重（对齐 macOS：封面来自首图时移除）
        var coverProxy = ConvertToProxyUrl(coverUrl);
        if (coverProxy != null && imageUrls.Count > 0 && imageUrls[0] == coverProxy)
            imageUrls.RemoveAt(0);

        // 清理正文：移除 HTML 标签 + 酷安表情（对齐 macOS）
        var cleanBody = RemoveHtmlTags(message ?? "");

        if (title == null && string.IsNullOrEmpty(cleanBody))
            return null;

        return BuildResult(title, cleanBody, username, coverProxy ?? coverUrl, imageUrls, originalUrl);
    }

    /// <summary>
    /// meta 标签提取（对齐 macOS extractFromMetaTags）
    /// </summary>
    private ParseResult? ExtractFromMetaTags(string html, ParseRequest request)
    {
        var title = ExtractMetaProperty(html, "og:title") ?? ExtractMetaName(html, "title");
        var desc = ExtractMetaProperty(html, "og:description") ?? ExtractMetaName(html, "description");
        var cover = ExtractMetaProperty(html, "og:image");
        var author = ExtractMetaName(html, "author") ?? ExtractMetaProperty(html, "og:article:author");

        if (title != null || desc != null || cover != null || author != null)
            return BuildResult(title, desc, author, cover, [], request.Url);

        return null;
    }

    private bool IsHighQualityContent(ParseResult result)
    {
        if (result.Content?.Title is { Length: > 5 } title &&
            !title.Contains("酷安") && title != "酷安APP")
            return true;
        if (result.Content?.Body is { Length: > 50 })
            return true;
        if (result.Content?.ImageUrls.Count > 0)
            return true;
        return false;
    }

    private ParseResult BuildResult(string? title, string? body, string? author, string? coverUrl, List<string> imageUrls, string url)
    {
        return BuildResult(title, body, author, coverUrl, imageUrls, new ParseRequest { Url = url });
    }

    private ParseResult BuildResult(string? title, string? body, string? author, string? coverUrl, List<string> imageUrls, ParseRequest request)
    {
        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = body,
            Author = author,
            CoverUrl = coverUrl,
            ImageUrls = imageUrls,
            PlatformContentId = UrlNormalizer.ExtractContentId(request.Url, Platform.coolapk),
            Platform = Platform.coolapk,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    private static string? ConvertToMirrorUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        var host = uri.Host.ToLowerInvariant();
        if (!host.Contains("coolapk.com") && !host.Contains("coolapk1s.com")) return null;
        var newUri = new UriBuilder(uri) { Host = "www.coolapk1s.com" }.Uri;
        return newUri.ToString();
    }

    private static string? ConvertToProxyUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var encoded = Uri.EscapeDataString(url);
        return $"https://image.coolapk1s.com/proxy?url={encoded}";
    }

    private static string RemoveHtmlTags(string input)
    {
        var cleaned = HtmlTagPattern().Replace(input, "");
        cleaned = CoolEmojiPattern().Replace(cleaned, "");
        return cleaned.Trim();
    }

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static string? ExtractMetaProperty(string html, string property)
    {
        var match = MetaPropertyPattern(property).Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractMetaName(string html, string name)
    {
        var match = MetaNamePattern(name).Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    #region Regex Patterns

    [GeneratedRegex("""<script id="__NEXT_DATA__" type="application/json">(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex NextDataPattern();

    [GeneratedRegex("""window\.__INITIAL_STATE__\s*=\s*(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex InitialStatePattern();

    [GeneratedRegex("""<[^>]+>""")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex("""\[[^\]]+\]""")]
    private static partial Regex CoolEmojiPattern();

    private static Regex MetaPropertyPattern(string property)
        => new($"""<meta[^>]+property="{property}"[^>]+content="([^"]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static Regex MetaNamePattern(string name)
        => new($"""<meta[^>]+name="{name}"[^>]+content="([^"]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static Match NextDataPatternForTest(string html) => NextDataPattern().Match(html);
    public static Match InitialStatePatternForTest(string html) => InitialStatePattern().Match(html);

    #endregion
}
