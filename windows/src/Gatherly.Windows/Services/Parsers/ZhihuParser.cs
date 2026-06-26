using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 知乎解析器 — HTTP 主路径，对齐 macOS ZhihuParser.swift
/// 回答: API → `__INITIAL_STATE__` → meta
/// 文章: API → `__INITIAL_STATE__` → meta
/// 暂不实现 WebView2 降级
/// </summary>
public partial class ZhihuParser : IContentParser
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1" },
            { "Accept", "application/json, text/html, */*" },
            { "Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8" },
            { "x-requested-with", "fetch" }
        }
    };

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.zhihu;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 回答 API（对齐 macOS fetchAnswerAPI）
            var answerId = ExtractAnswerId(request.Url);
            if (!string.IsNullOrEmpty(answerId))
                return await FetchAnswerApiAsync(answerId, request, cancellationToken);

            // 2. 文章 API（对齐 macOS fetchArticleAPI）
            var articleId = ExtractArticleId(request.Url);
            if (!string.IsNullOrEmpty(articleId))
                return await FetchArticleApiAsync(articleId, request, cancellationToken);

            // 3. 移动端页面（对齐 macOS parseMobilePage）
            return await ParseMobilePageAsync(request, cancellationToken);
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

    // === Answer API ===

    private async Task<ParseResult> FetchAnswerApiAsync(string answerId, ParseRequest request, CancellationToken ct)
    {
        var apiUrl = $"https://api.zhihu.com/answers/{answerId}?include=content,excerpt,question";
        var html = await SharedHttpClient.GetStringAsync(apiUrl, ct);
        var doc = JsonDocument.Parse(html);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var msg = GetString(error, "message") ?? "未知错误";
            return ParseResult.Fail($"知乎 API 错误: {msg}");
        }

        var apiContent = GetString(root, "content") ?? "";
        var isTruncated = root.TryGetProperty("content_need_truncated", out var trEl) && trEl.GetBoolean();

        string? questionTitle = null;
        if (root.TryGetProperty("question", out var question) && question.ValueKind == JsonValueKind.Object)
            questionTitle = GetString(question, "title");

        string? author = null, authorId = null;
        if (root.TryGetProperty("author", out var authorData) && authorData.ValueKind == JsonValueKind.Object)
        {
            author = GetString(authorData, "name");
            authorId = GetString(authorData, "url_token");
        }

        DateTimeOffset? publishDate = null;
        if (root.TryGetProperty("created_time", out var created) && created.TryGetDouble(out var ts))
            publishDate = DateTimeOffset.FromUnixTimeSeconds((long)ts);

        // 非截断或足够长时直接使用 API 内容
        var body = ConvertHtmlToMarkdown(apiContent);
        var imageUrls = ExtractImagesFromHtml(apiContent);

        var fullBody = !string.IsNullOrEmpty(questionTitle)
            ? $"**问题：** {questionTitle}\n\n{body}"
            : body;

        return ParseResult.Success(new ParsedContent
        {
            Title = questionTitle ?? "知乎回答",
            Body = fullBody,
            Author = author,
            AuthorId = authorId,
            PublishDate = publishDate,
            ImageUrls = imageUrls,
            PlatformContentId = answerId,
            Platform = Platform.zhihu,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    // === Article API ===

    private async Task<ParseResult> FetchArticleApiAsync(string articleId, ParseRequest request, CancellationToken ct)
    {
        var apiUrl = $"https://api.zhihu.com/articles/{articleId}?include=content,excerpt";
        var html = await SharedHttpClient.GetStringAsync(apiUrl, ct);
        var doc = JsonDocument.Parse(html);
        var root = doc.RootElement;

        if (root.TryGetProperty("error", out var error))
        {
            var msg = GetString(error, "message") ?? "未知错误";
            return ParseResult.Fail($"知乎 API 错误: {msg}");
        }

        var title = GetString(root, "title");
        var apiContent = GetString(root, "content") ?? "";

        string? author = null, authorId = null;
        if (root.TryGetProperty("author", out var authorData) && authorData.ValueKind == JsonValueKind.Object)
        {
            author = GetString(authorData, "name");
            authorId = GetString(authorData, "url_token");
        }

        DateTimeOffset? publishDate = null;
        if (root.TryGetProperty("created", out var created) && created.TryGetDouble(out var ts))
            publishDate = DateTimeOffset.FromUnixTimeSeconds((long)ts);

        var body = ConvertHtmlToMarkdown(apiContent);
        var imageUrls = ExtractImagesFromHtml(apiContent);

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = body,
            Author = author,
            AuthorId = authorId,
            PublishDate = publishDate,
            ImageUrls = imageUrls,
            PlatformContentId = articleId,
            Platform = Platform.zhihu,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    // === Mobile page ===

    private async Task<ParseResult> ParseMobilePageAsync(ParseRequest request, CancellationToken ct)
    {
        var mobileUrl = ToMobileUrl(request.Url);
        var html = await SharedHttpClient.GetStringAsync(mobileUrl, ct);

        // 提取 __INITIAL_STATE__ 或 js-initialData
        var jsonStr = ExtractInitialData(html);
        if (jsonStr != null)
        {
            var doc = JsonDocument.Parse(jsonStr);
            if (doc.RootElement.TryGetProperty("initialState", out var initialState) &&
                initialState.TryGetProperty("entities", out var entities))
            {
                // 回答
                if (entities.TryGetProperty("answers", out var answers) && answers.ValueKind == JsonValueKind.Object)
                {
                    var first = answers.EnumerateObject().FirstOrDefault();
                    if (first.Value.ValueKind == JsonValueKind.Object)
                    {
                        var answerId = first.Name;
                        return BuildAnswerFromDict(first.Value, answerId, request);
                    }
                }
                // 文章
                if (entities.TryGetProperty("articles", out var articles) && articles.ValueKind == JsonValueKind.Object)
                {
                    var first = articles.EnumerateObject().FirstOrDefault();
                    if (first.Value.ValueKind == JsonValueKind.Object)
                        return BuildArticleFromDict(first.Value, request);
                }
            }
        }

        // Meta fallback（对齐 macOS parseMetaFallback）
        return ParseMetaFallback(html, request);
    }

    private ParseResult BuildAnswerFromDict(JsonElement data, string answerId, ParseRequest request)
    {
        var content = GetString(data, "content") ?? "";
        var body = ConvertHtmlToMarkdown(content);

        string? questionTitle = null;
        if (data.TryGetProperty("question", out var question) && question.ValueKind == JsonValueKind.Object)
            questionTitle = GetString(question, "title");

        string? author = null, authorId = null;
        if (data.TryGetProperty("author", out var authorData) && authorData.ValueKind == JsonValueKind.Object)
        {
            author = GetString(authorData, "name");
            authorId = GetString(authorData, "urlToken") ?? GetString(authorData, "url_token");
        }

        DateTimeOffset? publishDate = null;
        if (data.TryGetProperty("createdTime", out var created) && created.TryGetDouble(out var ts))
            publishDate = DateTimeOffset.FromUnixTimeSeconds((long)ts);

        var fullBody = !string.IsNullOrEmpty(questionTitle)
            ? $"**问题：** {questionTitle}\n\n{body}"
            : body;

        return ParseResult.Success(new ParsedContent
        {
            Title = questionTitle ?? "知乎回答",
            Body = fullBody,
            Author = author,
            AuthorId = authorId,
            PublishDate = publishDate,
            ImageUrls = ExtractImagesFromHtml(content),
            PlatformContentId = answerId,
            Platform = Platform.zhihu,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    private ParseResult BuildArticleFromDict(JsonElement data, ParseRequest request)
    {
        var title = GetString(data, "title");
        var content = GetString(data, "content") ?? "";
        var body = ConvertHtmlToMarkdown(content);

        string? author = null, authorId = null;
        if (data.TryGetProperty("author", out var authorData) && authorData.ValueKind == JsonValueKind.Object)
        {
            author = GetString(authorData, "name");
            authorId = GetString(authorData, "urlToken") ?? GetString(authorData, "url_token");
        }

        DateTimeOffset? publishDate = null;
        if (data.TryGetProperty("created", out var created) && created.TryGetDouble(out var ts))
            publishDate = DateTimeOffset.FromUnixTimeSeconds((long)ts);

        var articleId = ExtractArticleId(request.Url);

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = body,
            Author = author,
            AuthorId = authorId,
            PublishDate = publishDate,
            ImageUrls = ExtractImagesFromHtml(content),
            PlatformContentId = articleId,
            Platform = Platform.zhihu,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    // === Meta fallback ===

    private ParseResult ParseMetaFallback(string html, ParseRequest request)
    {
        var title = ExtractMetaProperty(html, "og:title") ?? ExtractMetaName(html, "title");
        var desc = ExtractMetaProperty(html, "og:description") ?? ExtractMetaName(html, "description");
        var cover = ExtractMetaProperty(html, "og:image");

        var contentId = request.Url.Contains("/answer/")
            ? ExtractAnswerId(request.Url)
            : ExtractArticleId(request.Url);

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = desc,
            CoverUrl = cover,
            PlatformContentId = contentId,
            Platform = Platform.zhihu,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    // === URL helpers ===

    private static string ToMobileUrl(string url)
    {
        var result = url.Replace("://www.zhihu.com", "://m.zhihu.com");
        if (result.Contains("zhuanlan.zhihu.com/p/"))
            result = result.Replace("zhuanlan.zhihu.com/p/", "m.zhihu.com/article/");
        return result;
    }

    private static string? ExtractAnswerId(string url)
    {
        var match = AnswerIdPattern().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractArticleId(string url)
    {
        var match = ArticleIdPattern().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    // === HTML extraction ===

    private static string? ExtractInitialData(string html)
    {
        var match1 = InitialStatePattern().Match(html);
        if (match1.Success)
        {
            // 匹配到 window.__INITIAL_STATE__={...}，提取 JSON 对象
            return ExtractJsonFromBraces(html, match1.Groups[1].Index);
        }

        var match2 = JsInitialDataPattern().Match(html);
        if (match2.Success)
        {
            return match2.Groups[1].Value;
        }

        return null;
    }

    private static string? ExtractJsonFromBraces(string html, int startIndex)
    {
        var braceCount = 0;
        var foundOpening = false;
        var i = startIndex;
        while (i < html.Length)
        {
            if (html[i] == '{') { braceCount++; foundOpening = true; }
            else if (html[i] == '}') braceCount--;
            if (foundOpening && braceCount == 0) break;
            i++;
        }
        return foundOpening && braceCount == 0 ? html[startIndex..i] : null;
    }

    // === HTML to Markdown (对齐 macOS convertHTMLToMarkdown) ===

    public static string ConvertHtmlToMarkdown(string html)
    {
        var text = ImgDataOriginalPattern().Replace(html, "![]($1)");
        text = ImgDataActualsrcPattern().Replace(text, "![]($1)");
        text = ImgSrcPattern().Replace(text, "![]($1)");
        text = BrPattern().Replace(text, "\n");
        text = ClosingPTagPattern().Replace(text, "\n\n");
        text = ClosingDivTagPattern().Replace(text, "\n");
        text = HeadingOpenPattern().Replace(text, "\n## ");
        text = HeadingClosePattern().Replace(text, "\n");
        text = BoldOpenPattern().Replace(text, "**");
        text = BoldClosePattern().Replace(text, "**");
        text = StrongOpenPattern().Replace(text, "**");
        text = StrongClosePattern().Replace(text, "**");
        text = LiOpenPattern().Replace(text, "- ");
        text = ClosingLiTagPattern().Replace(text, "\n");
        text = HtmlTagPattern().Replace(text, "");
        text = text.Replace("&amp;", "&");
        text = text.Replace("&lt;", "<");
        text = text.Replace("&gt;", ">");
        text = text.Replace("&nbsp;", " ");
        text = text.Replace("&quot;", "\"");
        text = text.Replace("&#39;", "'");
        text = Newline3PlusPattern().Replace(text, "\n\n");
        return text.Trim();
    }

    public static List<string> ExtractImagesFromHtml(string html)
    {
        var urls = new List<string>();
        foreach (var match in ImgDataOriginalPattern().Matches(html).Cast<Match>())
            if (match.Groups.Count > 1) AddUnique(urls, match.Groups[1].Value);
        foreach (var match in ImgDataActualsrcPattern().Matches(html).Cast<Match>())
            if (match.Groups.Count > 1) AddUnique(urls, match.Groups[1].Value);
        foreach (var match in ImgSrcHttpPattern().Matches(html).Cast<Match>())
            if (match.Groups.Count > 1) AddUnique(urls, match.Groups[1].Value);
        return urls;
    }

    private static void AddUnique(List<string> list, string url)
    {
        if (!list.Contains(url)) list.Add(url);
    }

    // === Meta ===

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

    // === Helpers ===

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    #region Regex Patterns

    [GeneratedRegex("""zhihu\.com/question/\d+/answer/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex AnswerIdPattern();

    [GeneratedRegex("""zhihu\.com/p/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ArticleIdPattern();

    [GeneratedRegex("""window\.__INITIAL_STATE__\s*=\s*\{""")]
    private static partial Regex InitialStatePattern();

    [GeneratedRegex("""id="js-initialData"\s+type="text/json">(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex JsInitialDataPattern();

    [GeneratedRegex("""<img[^>]*data-original="([^"]*)"[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgDataOriginalPattern();

    [GeneratedRegex("""<img[^>]*data-actualsrc="([^"]*)"[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgDataActualsrcPattern();

    [GeneratedRegex("""<img[^>]*src="([^"]*)"[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcPattern();

    [GeneratedRegex("""(https?://[^"'\s]*\.(?:jpg|jpeg|png|gif|webp)[^"'\s]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcHttpPattern();

    [GeneratedRegex("""<br\s*/?>""", RegexOptions.IgnoreCase)]
    private static partial Regex BrPattern();

    [GeneratedRegex("""</p>""")]
    private static partial Regex ClosingPTagPattern();

    [GeneratedRegex("""</div>""")]
    private static partial Regex ClosingDivTagPattern();

    [GeneratedRegex("""<h[1-6][^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingOpenPattern();

    [GeneratedRegex("""</h[1-6]>""", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingClosePattern();

    [GeneratedRegex("""<b>""", RegexOptions.IgnoreCase)]
    private static partial Regex BoldOpenPattern();

    [GeneratedRegex("""</b>""", RegexOptions.IgnoreCase)]
    private static partial Regex BoldClosePattern();

    [GeneratedRegex("""<strong>""", RegexOptions.IgnoreCase)]
    private static partial Regex StrongOpenPattern();

    [GeneratedRegex("""</strong>""", RegexOptions.IgnoreCase)]
    private static partial Regex StrongClosePattern();

    [GeneratedRegex("""<li[^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex LiOpenPattern();

    [GeneratedRegex("""</li>""")]
    private static partial Regex ClosingLiTagPattern();

    [GeneratedRegex("""<[^>]+>""")]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex("""\n{3,}""")]
    private static partial Regex Newline3PlusPattern();

    private static Regex MetaPropertyPattern(string prop)
        => new($"""property="{prop}"\s+content="([^"]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static Regex MetaNamePattern(string name)
        => new($"""name="{name}"\s+content="([^"]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    #endregion
}
