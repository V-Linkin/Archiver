using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// B站解析器 — 使用 Web API 获取视频基础信息
/// 支持 BV / av 链接，支持 b23.tv 短链展开
/// </summary>
public partial class BilibiliParser : IContentParser
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
            { "Referer", "https://www.bilibili.com/" }
        }
    };

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.bilibili;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var resolvedUrl = await ResolveShortUrlAsync(request.Url, cancellationToken);

            var hasBvid = TryExtractBvId(resolvedUrl, out var bvid);
            var hasAvid = TryExtractAvId(resolvedUrl, out var avid);
            if (!hasBvid && !hasAvid)
                return ParseResult.Fail("无法从 URL 提取 B站视频 ID");

            // 尝试 API
            var apiResult = hasBvid
                ? await FetchFromApiAsync($"https://api.bilibili.com/x/web-interface/view?bvid={bvid}", cancellationToken)
                : await FetchFromApiAsync($"https://api.bilibili.com/x/web-interface/view?aid={avid}", cancellationToken);

            if (apiResult != null)
                return apiResult;

            // Fallback: HTML meta
            return await FetchFromHtmlAsync(resolvedUrl, request, cancellationToken);
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

    private async Task<ParseResult?> FetchFromApiAsync(string apiUrl, CancellationToken ct)
    {
        var jsonString = await SharedHttpClient.GetStringAsync(apiUrl, ct);
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        var code = GetInt64(root, "code");
        if (code != 0) return null;

        if (!root.TryGetProperty("data", out var data))
            return null;

        var title = GetString(data, "title") ?? "未知标题";
        var desc = GetString(data, "desc") ?? "";
        var bvid = GetString(data, "bvid") ?? "";
        var pubdate = GetInt64(data, "pubdate") ?? 0;

        var coverUrl = GetString(data, "pic");
        if (!string.IsNullOrEmpty(coverUrl))
        {
            if (coverUrl.StartsWith("//")) coverUrl = "https:" + coverUrl;
            else if (coverUrl.StartsWith("http://")) coverUrl = coverUrl.Replace("http://", "https://");
        }

        string? author = null;
        string? authorId = null;
        if (data.TryGetProperty("owner", out var owner))
        {
            author = GetString(owner, "name");
            authorId = GetInt64(owner, "mid")?.ToString();
        }

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = string.IsNullOrEmpty(desc) ? null : desc,
            Author = author ?? "未知作者",
            AuthorId = authorId,
            PublishDate = pubdate > 0 ? DateTimeOffset.FromUnixTimeSeconds(pubdate) : null,
            CoverUrl = coverUrl,
            Platform = Platform.bilibili,
            PlatformContentId = bvid,
            OriginalUrl = null,
            NormalizedUrl = null
        });
    }

    private async Task<ParseResult> FetchFromHtmlAsync(string url, ParseRequest request, CancellationToken ct)
    {
        var html = await SharedHttpClient.GetStringAsync(url, ct);

        var title = ExtractMeta(html, "og:title");
        if (string.IsNullOrEmpty(title))
            title = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase).Groups[1].Value;

        var description = ExtractMeta(html, "og:description");
        var coverUrl = ExtractMeta(html, "og:image");

        if (string.IsNullOrEmpty(title))
            return ParseResult.Fail("无法从页面提取标题");

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = string.IsNullOrEmpty(description) ? null : description,
            Author = "未知作者",
            CoverUrl = coverUrl,
            Platform = Platform.bilibili,
            PlatformContentId = request.PlatformContentId,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    private static bool TryExtractBvId(string url, out string? bvid)
    {
        var match = Regex.Match(url, @"bilibili\.com/video/(BV[a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            bvid = match.Groups[1].Value;
            return true;
        }
        bvid = null;
        return false;
    }

    private static bool TryExtractAvId(string url, out string? avid)
    {
        var match = Regex.Match(url, @"bilibili\.com/video/(av\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            avid = match.Groups[1].Value;
            return true;
        }
        avid = null;
        return false;
    }

    private async Task<string> ResolveShortUrlAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != "b23.tv")
            return url;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await SharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            return response.RequestMessage?.RequestUri?.ToString() ?? url;
        }
        catch
        {
            return url;
        }
    }

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
            return null;

        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static long? GetInt64(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
            return null;

        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var value))
            return value;

        if (el.ValueKind == JsonValueKind.String && long.TryParse(el.GetString(), out var parsed))
            return parsed;

        return null;
    }

    private static string ExtractMeta(string html, string property)
    {
        var pattern = $@"{property}""\s+content=""([^""]*)""";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }
}
