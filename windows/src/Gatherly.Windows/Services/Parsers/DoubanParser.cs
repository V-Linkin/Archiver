using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 豆瓣解析器 — HTTP 主路径，对齐 macOS DoubanParser.swift
/// 三级解析：Subject（书/影/音）→ Review（影评/书评）→ Generic（兜底）
/// 暂不实现 WKWebView 降级
/// </summary>
public partial class DoubanParser : IContentParser
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1" },
            { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
            { "Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8" }
        }
    };

    private static readonly HttpClient DesktopHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
            { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" },
            { "Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8" },
            { "Referer", "https://movie.douban.com/" }
        }
    };

    // 请求频率限制（对齐 macOS DoubanRateLimiter，2 秒间隔）
    private static DateTime _lastRequestTime = DateTime.MinValue;
    private static readonly object _rateLock = new();
    private static void RespectRateLimit()
    {
        lock (_rateLock)
        {
            var elapsed = (DateTime.UtcNow - _lastRequestTime).TotalSeconds;
            var wait = Math.Max(0, 2.0 - elapsed);
            if (wait > 0) Thread.Sleep(TimeSpan.FromSeconds(wait));
            _lastRequestTime = DateTime.UtcNow;
        }
    }

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.douban;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            if (IsSubjectUrl(request.Url))
                return await ParseSubjectPageAsync(request, cancellationToken);

            if (IsReviewUrl(request.Url))
                return await ParseReviewPageAsync(request, cancellationToken);

            return await ParseGenericPageAsync(request, cancellationToken);
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

    // === Subject Page (书/影/音) ===

    private async Task<ParseResult> ParseSubjectPageAsync(ParseRequest request, CancellationToken ct)
    {
        RespectRateLimit();
        var mobileUrl = ToMobileUrl(request.Url);
        var html = await SharedHttpClient.GetStringAsync(mobileUrl, ct);

        var subjectId = UrlNormalizer.ExtractDoubanId(request.Url);
        var title = ExtractMetaProperty(html, "og:title") ?? ExtractPageTitle(html);
        var cover = ExtractMetaProperty(html, "og:image");
        var director = ExtractDirector(html);
        var author = ExtractAuthor(html);
        var authorName = director ?? author;

        string? rating = null;
        var ratingValue = ExtractMetaItemprop(html, "ratingValue");
        if (!string.IsNullOrEmpty(ratingValue))
            rating = $"豆瓣评分: {ratingValue}";

        var fullIntro = ExtractSubjectIntro(html);
        var desc = ExtractMetaProperty(html, "og:description") ?? ExtractMetaName(html, "description");

        var body = !string.IsNullOrEmpty(fullIntro) ? fullIntro : (desc ?? "");
        if (!string.IsNullOrEmpty(rating))
            body = string.IsNullOrEmpty(body) ? rating : $"{rating}\n\n{body}";

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = string.IsNullOrEmpty(body) ? null : body,
            Author = authorName,
            CoverUrl = cover,
            PlatformContentId = subjectId,
            Platform = Platform.douban,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    // === Review Page (影评/书评) ===

    private async Task<ParseResult> ParseReviewPageAsync(ParseRequest request, CancellationToken ct)
    {
        RespectRateLimit();

        var reviewId = UrlNormalizer.ExtractDoubanId(request.Url);
        var html = await DesktopHttpClient.GetStringAsync(request.Url, ct);

        var metaTitle = ExtractMetaProperty(html, "og:title") ?? ExtractPageTitle(html);
        var metaDesc = ExtractMetaProperty(html, "og:description") ?? ExtractMetaName(html, "description");
        var metaAuthor = ExtractMetaName(html, "author");

        var htmlBody = ExtractReviewBodyFromHtml(html);
        var bodyImageUrls = ExtractReviewImageUrls(html);
        var metaCover = ExtractMetaProperty(html, "og:image");

        var reviewAuthor = ExtractReviewAuthorFromHtml(html) ?? metaAuthor;

        // 标题：兜底 h1
        var title = metaTitle;
        if (title == "豆瓣" || (title != null && title.Length < 3))
        {
            var h1 = ExtractFirst(html, H1Pattern());
            if (h1 != null) title = h1.Trim();
        }

        // 正文：优先 HTML 解析，兜底 meta description
        var body = htmlBody ?? metaDesc;

        // 封面：优先 JSON-LD 电影海报
        var cover = ExtractMoviePosterFromReviewHtml(html) ?? metaCover;

        // bodyImageURLs 存入 rawMetadata
        var metadata = new Dictionary<string, string> { ["type"] = "review" };
        if (bodyImageUrls.Count > 0)
            metadata["bodyImageURLs"] = string.Join("||", bodyImageUrls);

        // 验证 HTTP 结果有效性
        var httpMeaningful = !string.IsNullOrEmpty(body) && body.Length > 30
            || !string.IsNullOrEmpty(title) && title != "豆瓣" && title.Length > 5
            || !string.IsNullOrEmpty(reviewAuthor)
            || bodyImageUrls.Count > 0;

        if (!httpMeaningful)
        {
            // HTTP 未获取到有效内容，尝试 WebView2（对齐 macOS WKWebView 无条件调用）
            var webResult = await TryLoadReviewViaWebView2(request.Url, ct);

            // 从 WebView2 的 custom JS 结果中提取结构化数据
            if (webResult?.ScriptResults.TryGetValue("custom", out var diagJson) == true && !string.IsNullOrEmpty(diagJson))
            {
                try
                {
                    var diagDoc = JsonDocument.Parse(diagJson);
                    var diagRoot = diagDoc.RootElement;

                    var jsTitle = diagRoot.TryGetProperty("title", out var tEl) && tEl.ValueKind == JsonValueKind.String ? tEl.GetString() : null;
                    var jsBody = diagRoot.TryGetProperty("body", out var bEl) && bEl.ValueKind == JsonValueKind.String ? bEl.GetString() : null;
                    var jsAuthor = diagRoot.TryGetProperty("author", out var aEl) && aEl.ValueKind == JsonValueKind.String ? aEl.GetString() : null;
                    var jsCover = diagRoot.TryGetProperty("cover", out var cEl) && cEl.ValueKind == JsonValueKind.String ? cEl.GetString() : null;
                    var jsBodySelector = diagRoot.TryGetProperty("bodySelector", out var sEl) && sEl.ValueKind == JsonValueKind.String ? sEl.GetString() : null;

                    if (!string.IsNullOrEmpty(jsTitle) && jsTitle != "豆瓣")
                        title = jsTitle;
                    if (!string.IsNullOrEmpty(jsBody) && jsBody.Length > 30)
                        body = jsBody;
                    if (!string.IsNullOrEmpty(jsAuthor))
                        reviewAuthor = jsAuthor;
                    if (!string.IsNullOrEmpty(jsCover))
                        cover = jsCover;

                    metadata["source"] = "webview";
                    if (!string.IsNullOrEmpty(jsBodySelector))
                        metadata["bodySelector"] = jsBodySelector;
                }
                catch { }
            }
        }

        // 最终有效性检查
        var finalMeaningful = !string.IsNullOrEmpty(body) && body.Length > 30
            || !string.IsNullOrEmpty(title) && title != "豆瓣" && title.Length > 5
            || !string.IsNullOrEmpty(reviewAuthor)
            || bodyImageUrls.Count > 0;

        if (!finalMeaningful)
        {
            return ParseResult.Fail("豆瓣影评页面无法获取有效内容，可能需要登录或验证码");
        }

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = body,
            Author = reviewAuthor,
            CoverUrl = cover,
            PlatformContentId = reviewId,
            Platform = Platform.douban,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    private async Task<Web.RenderedPageResult?> TryLoadReviewViaWebView2(string url, CancellationToken ct)
    {
        try
        {
            var loader = new Web.JsRenderedPageLoader();
            // 用 JS 从 DOM 提取结构化数据（对齐 macOS loadReviewViaWebView 的 selector 方案）
            var extractJs = @"(() => {
                var pickText = function(sels) {
                    for (var i = 0; i < sels.length; i++) {
                        var el = document.querySelector(sels[i]);
                        if (el) {
                            var t = (el.innerText || '').trim();
                            if (t.length > 50) return {selector: sels[i], text: t};
                        }
                    }
                    return {selector: '', text: ''};
                };
                var body = pickText(['.review-content','#link-report','.main-bd','.main-review','[property=""v:description""]']);
                var titleEl = document.querySelector('h1') || document.querySelector('.main-title');
                var title = titleEl ? titleEl.innerText.trim() : document.title;
                var authorEl = document.querySelector('.main-hd a[href*=""people""]') || document.querySelector('[rel=""author""]') || document.querySelector('[itemprop=""author""]');
                var author = authorEl ? authorEl.innerText.trim() : '';
                var images = Array.from(document.querySelectorAll('img')).map(function(i){return i.currentSrc||i.src||''}).filter(function(s){return s.indexOf('doubanio')>=0||s.indexOf('douban')>=0;});
                var posterImgs = Array.from(document.querySelectorAll('img')).filter(function(i){
                    var s=i.currentSrc||i.src||'';
                    var c=typeof i.className==='string'?i.className:'';
                    return (s.indexOf('doubanio')>=0||s.indexOf('douban')>=0) && (s.indexOf('poster')>=0||s.indexOf('subject')>=0||c.indexOf('poster')>=0);
                });
                var cover = posterImgs.length>0 ? posterImgs[0].currentSrc||posterImgs[0].src||'' : (images.length>0 ? images[0] : '');
                return {
                    title: title,
                    bodySelector: body.selector,
                    body: body.text,
                    author: author,
                    images: images,
                    cover: cover,
                    textLen: (document.body.innerText || '').length
                };
            })()";

            return await loader.LoadAsync(url, new Web.RenderedPageOptions
            {
                TimeoutSeconds = 20,
                ExtraWaitMs = 3000,
                GetHtml = true,
                GetInnerText = true,
                GetTitle = true,
                JavaScriptExpression = extractJs
            }, ct);
        }
        catch { return null; }
    }

    // === Generic Page ===

    private async Task<ParseResult> ParseGenericPageAsync(ParseRequest request, CancellationToken ct)
    {
        RespectRateLimit();
        var mobileUrl = ToMobileUrl(request.Url);
        var html = await SharedHttpClient.GetStringAsync(mobileUrl, ct);

        var title = ExtractMetaProperty(html, "og:title") ?? ExtractPageTitle(html);
        var desc = ExtractMetaProperty(html, "og:description") ?? ExtractMetaName(html, "description");
        var cover = ExtractMetaProperty(html, "og:image");
        var subjectId = UrlNormalizer.ExtractDoubanId(request.Url);

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = desc,
            Author = ExtractMetaName(html, "author"),
            CoverUrl = cover,
            PlatformContentId = subjectId,
            Platform = Platform.douban,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    // === URL Helpers ===

    private static bool IsSubjectUrl(string url) => url.Contains("/subject/");
    private static bool IsReviewUrl(string url) => url.Contains("/review/");

    private static string ToMobileUrl(string url)
    {
        var result = url;
        if (result.Contains("://movie.douban.com/"))
            result = result.Replace("://movie.douban.com/", "://m.douban.com/movie/");
        else if (result.Contains("://book.douban.com/"))
            result = result.Replace("://book.douban.com/", "://m.douban.com/book/");
        else if (result.Contains("://music.douban.com/"))
            result = result.Replace("://music.douban.com/", "://m.douban.com/music/");
        else if (result.Contains("://www.douban.com/"))
            result = result.Replace("://www.douban.com/", "://m.douban.com/");
        else if (result.Contains("://douban.com/"))
            result = result.Replace("://douban.com/", "://m.douban.com/");
        return result;
    }

    // === HTML Extraction ===

    private static string? ExtractSubjectIntro(string html)
    {
        var match = SubjectIntroPattern().Match(html);
        if (!match.Success) return null;
        var text = CleanHtml(match.Groups[1].Value);
        foreach (var prefix in new[] { "剧情简介", "内容简介", "简介", "作品简介", "图书简介" })
            if (text.StartsWith(prefix)) { text = text[prefix.Length..].Trim(); break; }
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? ExtractDirector(string html)
    {
        foreach (var p in new[] { DirectorPattern1(), DirectorPattern2(), DirectorPattern3() })
        {
            var m = p.Match(html);
            if (m.Success) return m.Groups[1].Value.Trim();
        }
        return null;
    }

    private static string? ExtractAuthor(string html)
    {
        foreach (var p in new[] { AuthorPattern1(), AuthorPattern2() })
        {
            var m = p.Match(html);
            if (m.Success) return m.Groups[1].Value.Trim();
        }
        return null;
    }

    private static string? ExtractReviewBodyFromHtml(string html)
    {
        foreach (var attr in new[] { "review-content", "link-report", "main-review", "review-body" })
        {
            var content = ExtractNestedDivContent(html, attr);
            if (content != null)
            {
                var text = CleanHtml(content);
                if (text.Length > 30) return text;
            }
        }
        return null;
    }

    private static List<string> ExtractReviewImageUrls(string html)
    {
        var bodyHtml = "";
        foreach (var attr in new[] { "review-content", "link-report", "main-review" })
        {
            var content = ExtractNestedDivContent(html, attr);
            if (!string.IsNullOrEmpty(content)) { bodyHtml = content; break; }
        }
        if (string.IsNullOrEmpty(bodyHtml)) bodyHtml = html;

        var urls = new List<string>();
        foreach (Match m in ImgSrcPattern().Matches(bodyHtml))
        {
            var src = m.Groups[1].Value.Trim();
            if (src.Contains("doubanio.com") || src.Contains("douban.com"))
            {
                src = src.Replace("/s_ratio_poster/", "/raw/").Replace("/m_ratio_poster/", "/raw/").Replace("/s_crop_poster/", "/raw/");
                if (src.StartsWith("//")) src = "https:" + src;
                if (!urls.Contains(src)) urls.Add(src);
            }
        }
        foreach (Match m in ImgDataSrcPattern().Matches(bodyHtml))
        {
            var src = m.Groups[1].Value.Trim();
            if (src.Contains("doubanio.com") || src.Contains("douban.com"))
            {
                if (src.StartsWith("//")) src = "https:" + src;
                if (!urls.Contains(src)) urls.Add(src);
            }
        }
        return urls;
    }

    private static string? ExtractReviewAuthorFromHtml(string html)
    {
        // JSON-LD
        foreach (Match m in JsonLdPattern().Matches(html))
        {
            try
            {
                var json = JsonDocument.Parse(m.Groups[1].Value);
                if (json.RootElement.TryGetProperty("author", out var author) &&
                    author.TryGetProperty("name", out var name))
                {
                    var n = name.GetString();
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch { }
        }
        // data-author
        var da = ExtractFirst(html, DataAuthorPattern());
        if (da != null && da.Trim().Length is > 0 and < 30 && da.Trim() != "豆瓣")
            return da.Trim();
        // header area people link
        var headerMatch = HeaderMainHdPattern().Match(html);
        if (headerMatch.Success)
        {
            var start = headerMatch.Index;
            var length = Math.Min(3000, html.Length - start);
            var headerArea = html.Substring(start, length);
            var pl = ExtractFirst(headerArea, PeopleLinkPattern());
            if (pl != null && pl.Trim().Length is >= 2 and < 30 && pl.Trim() != "豆瓣")
                return pl.Trim();
        }
        return null;
    }

    private static string? ExtractMoviePosterFromReviewHtml(string html)
    {
        foreach (Match m in JsonLdPattern().Matches(html))
        {
            try
            {
                var json = JsonDocument.Parse(m.Groups[1].Value);
                if (json.RootElement.TryGetProperty("itemReviewed", out var ir) &&
                    ir.TryGetProperty("image", out var img) &&
                    img.ValueKind == JsonValueKind.String)
                {
                    var url = img.GetString();
                    if (!string.IsNullOrEmpty(url)) return url;
                }
            }
            catch { }
        }
        return null;
    }

    /// <summary>
    /// 追踪嵌套 div 提取内容（对齐 macOS extractNestedDivContent）
    /// </summary>
    private static string? ExtractNestedDivContent(string html, string attrPattern)
    {
        var divMatch = DivWithAttrPattern(attrPattern).Match(html);
        if (!divMatch.Success) return null;

        var startIdx = divMatch.Index + divMatch.Length;
        var depth = 1;
        var i = startIdx;
        while (i < html.Length && depth > 0)
        {
            if (i + 4 <= html.Length && html.Substring(i, 4) is "<div" or "<DIV")
            {
                if (i > 0 && html[i - 1] == '/')
                {
                    depth--;
                    if (depth == 0) break;
                }
                else depth++;
            }
            else if (i + 6 <= html.Length && html.Substring(i, 6) is "</div>" or "</DIV>")
            {
                depth--;
                if (depth == 0) break;
                i += 6;
                continue;
            }
            i++;
        }
        return depth == 0 ? html.Substring(startIdx, i - startIdx) : null;
    }

    // === HTML Cleaning (对齐 macOS cleanHTML) ===

    public static string CleanHtml(string html)
    {
        var text = BrPattern().Replace(html, "\n");
        text = ClosingPPattern().Replace(text, "\n\n");
        text = ClosingDivPattern().Replace(text, "\n");
        text = HeadingClosePattern().Replace(text, "\n");
        text = ClosingLiPattern().Replace(text, "\n");
        text = ClosingTrPattern().Replace(text, "\n");
        text = LiOpenPattern().Replace(text, "• ");
        text = POpenPattern().Replace(text, "");
        text = HeadingOpenPattern().Replace(text, "\n");
        text = HtmlTagPattern().Replace(text, "");
        text = text.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
            .Replace("&nbsp;", " ").Replace("&quot;", "\"").Replace("&#39;", "'")
            .Replace("&hellip;", "…").Replace("&mdash;", "—").Replace("&ndash;", "–");
        text = TrailingWhitespaceLinePattern().Replace(text, "\n");
        text = LeadingWhitespaceLinePattern().Replace(text, "\n");
        text = MultiSpacePattern().Replace(text, " ");
        text = MultiNewlinePattern().Replace(text, "\n\n");
        return text.Trim();
    }

    // === Meta Helpers ===

    private static string? ExtractMetaProperty(string html, string prop) =>
        ExtractFirst(html, MetaPropertyPattern(prop));

    private static string? ExtractMetaName(string html, string name) =>
        ExtractFirst(html, MetaNamePattern(name));

    private static string? ExtractMetaItemprop(string html, string name) =>
        ExtractFirst(html, MetaItempropPattern(name));

    private static string? ExtractPageTitle(string html)
    {
        var m = PageTitlePattern().Match(html);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractFirst(string text, Regex regex)
    {
        var m = regex.Match(text);
        return m.Success && m.Groups.Count > 1 ? m.Groups[1].Value : null;
    }

    #region Regex Patterns

    [GeneratedRegex("""<section class="subject-intro">(.*?)</section>""", RegexOptions.Singleline)]
    private static partial Regex SubjectIntroPattern();

    [GeneratedRegex("""<span[^>]*class="info-item-key"[^>]*>\s*导演\s*:?</span>\s*<span[^>]*class="info-item-val"[^>]*>([^<]+)</span>""", RegexOptions.Singleline)]
    private static partial Regex DirectorPattern1();
    [GeneratedRegex("""<span class="pl">导演</span>.*?<a[^>]*>([^<]+)</a>""", RegexOptions.Singleline)]
    private static partial Regex DirectorPattern2();
    [GeneratedRegex("""<span[^>]*>导演\s*:</span>\s*<span[^>]*>([^<]+)</span>""", RegexOptions.Singleline)]
    private static partial Regex DirectorPattern3();

    [GeneratedRegex("""<span[^>]*class="info-item-key"[^>]*>\s*作者\s*:?</span>\s*<span[^>]*class="info-item-val"[^>]*>([^<]+)</span>""", RegexOptions.Singleline)]
    private static partial Regex AuthorPattern1();
    [GeneratedRegex("""<span class="pl">作者</span>.*?<a[^>]*>([^<]+)</a>""", RegexOptions.Singleline)]
    private static partial Regex AuthorPattern2();

    [GeneratedRegex("""<h1[^>]*>([^<]+)</h1>""")]
    private static partial Regex H1Pattern();

    [GeneratedRegex("""<script\s+type="application/ld\+json">(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex JsonLdPattern();

    [GeneratedRegex("""data-author="([^"]+)""")]
    private static partial Regex DataAuthorPattern();

    [GeneratedRegex("""<header class="main-hd">""")]
    private static partial Regex HeaderMainHdPattern();

    [GeneratedRegex("""<a[^>]*href="[^"]*douban\.com/people/[^"]*"[^>]*>([^<]+)</a>""")]
    private static partial Regex PeopleLinkPattern();

    [GeneratedRegex("""<img[^>]*\bsrc="([^"]*)"[^>]*/?>""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSrcPattern();

    [GeneratedRegex("""<img[^>]*\bdata-src="([^"]*)"[^>]*/?>""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgDataSrcPattern();

    [GeneratedRegex("""<div[^>]*class="([^"]*)""")]
    private static partial Regex PlaceholderRegex();

    private static Regex DivWithAttrPattern(string attr)
        => new($"""<div[^>]*class="[^"]*{attr}[^"]*"[^>]*>""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [GeneratedRegex("""<br\s*/?>""")]
    private static partial Regex BrPattern();
    [GeneratedRegex("""</p>""")]
    private static partial Regex ClosingPPattern();
    [GeneratedRegex("""</div>""")]
    private static partial Regex ClosingDivPattern();
    [GeneratedRegex("""</h[1-6]>""")]
    private static partial Regex HeadingClosePattern();
    [GeneratedRegex("""</li>""")]
    private static partial Regex ClosingLiPattern();
    [GeneratedRegex("""</tr>""")]
    private static partial Regex ClosingTrPattern();
    [GeneratedRegex("""<li[^>]*>""")]
    private static partial Regex LiOpenPattern();
    [GeneratedRegex("""<p[^>]*>""")]
    private static partial Regex POpenPattern();
    [GeneratedRegex("""<h[1-6][^>]*>""")]
    private static partial Regex HeadingOpenPattern();
    [GeneratedRegex("""<[^>]+>""")]
    private static partial Regex HtmlTagPattern();
    [GeneratedRegex("""\n[ \t]+""")]
    private static partial Regex TrailingWhitespaceLinePattern();
    [GeneratedRegex("""[ \t]+\n""")]
    private static partial Regex LeadingWhitespaceLinePattern();
    [GeneratedRegex("""[ \t]{2,}""")]
    private static partial Regex MultiSpacePattern();
    [GeneratedRegex("""\n{3,}""")]
    private static partial Regex MultiNewlinePattern();

    [GeneratedRegex("""property="([^"]+)"\s+content="([^"]*)" """, RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderMetaProp();
    private static Regex MetaPropertyPattern(string p) => new($"""property="{p}"\s+content="([^"]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static Regex MetaNamePattern(string n) => new($"""name="{n}"\s+content="([^"]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static Regex MetaItempropPattern(string n) => new($"""itemprop="{n}"\s+content="([^"]*)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static Regex PageTitlePattern() => new("""<title>([^<]*)</title>""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    #endregion
}
