using System.Text.Json;
using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// YouTube 解析器 — 使用 HTML meta + ytInitialPlayerResponse 解析视频信息
/// 支持 watch / youtu.be / shorts 链接
/// </summary>
public partial class YouTubeParser : IContentParser
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" },
            { "Accept-Language", "en-US,en;q=0.9" },
            { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8" }
        }
    };

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.youtube;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var videoId = ExtractVideoId(request.Url);
            if (string.IsNullOrEmpty(videoId))
                return ParseResult.Fail("无法从 URL 提取 YouTube 视频 ID");

            var html = await SharedHttpClient.GetStringAsync(request.Url, cancellationToken);

            // 策略 1: ytInitialPlayerResponse JSON
            var playerResult = TryParsePlayerResponse(html, videoId);
            if (playerResult != null)
                return playerResult;

            // 策略 2: meta tags
            var metaResult = TryParseMetaTags(html, request, videoId);
            if (metaResult != null)
                return metaResult;

            // 策略 3: 最小 fallback
            var title = ExtractMeta(html, "og:title").Replace(" - YouTube", "").Trim();
            if (string.IsNullOrEmpty(title))
                title = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase).Groups[1].Value.Replace(" - YouTube", "").Trim();

            return ParseResult.Success(new ParsedContent
            {
                Title = string.IsNullOrEmpty(title) ? null : title,
                Platform = Platform.youtube,
                PlatformContentId = videoId,
                OriginalUrl = request.Url,
                NormalizedUrl = request.NormalizedUrl
            });
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

    private ParseResult? TryParsePlayerResponse(string html, string videoId)
    {
        var jsonString = ExtractJson(html, "ytInitialPlayerResponse");
        if (jsonString == null) return null;

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            if (!root.TryGetProperty("videoDetails", out var videoDetails))
                return null;

            var title = GetString(videoDetails, "title");
            var author = GetString(videoDetails, "author");
            var shortDesc = GetString(videoDetails, "shortDescription");
            var lengthSeconds = GetInt64(videoDetails, "lengthSeconds");
            var viewCount = GetInt64(videoDetails, "viewCount");

            // 封面: thumbnail → thumbnails 数组最后一个
            string? coverUrl = null;
            if (videoDetails.TryGetProperty("thumbnail", out var thumbnail) &&
                thumbnail.TryGetProperty("thumbnails", out var thumbnails) &&
                thumbnails.GetArrayLength() > 0)
            {
                coverUrl = GetString(thumbnails[thumbnails.GetArrayLength() - 1], "url");
            }

            // microformat → publishDate
            DateTimeOffset? publishDate = null;
            if (root.TryGetProperty("microformat", out var microformat) &&
                microformat.TryGetProperty("playerMicroformatRenderer", out var renderer))
            {
                var dateStr = GetString(renderer, "publishDate");
                if (dateStr != null && DateOnly.TryParse(dateStr, out var date))
                    publishDate = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            }

            var bodyParts = new List<string>();
            if (!string.IsNullOrEmpty(shortDesc))
                bodyParts.Add(shortDesc);
            if (viewCount > 0)
                bodyParts.Add($"播放量: {viewCount}");
            if (lengthSeconds > 0)
            {
                var minutes = lengthSeconds.Value / 60;
                var secs = lengthSeconds.Value % 60;
                bodyParts.Add($"时长: {minutes}:{secs:D2}");
            }

            return ParseResult.Success(new ParsedContent
            {
                Title = title,
                Body = bodyParts.Count > 0 ? string.Join("\n\n", bodyParts) : null,
                Author = author,
                AuthorId = author,
                PublishDate = publishDate,
                CoverUrl = coverUrl,
                Platform = Platform.youtube,
                PlatformContentId = videoId,
                OriginalUrl = null,
                NormalizedUrl = null
            });
        }
        catch
        {
            return null;
        }
    }

    private ParseResult? TryParseMetaTags(string html, ParseRequest request, string videoId)
    {
        var title = ExtractMeta(html, "og:title");
        if (string.IsNullOrEmpty(title))
        {
            title = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase).Groups[1].Value;
            title = title.Replace(" - YouTube", "").Trim();
        }

        if (string.IsNullOrEmpty(title))
            return null;

        var description = ExtractMeta(html, "og:description");
        var coverUrl = ExtractMeta(html, "og:image");
        var author = ExtractMeta(html, "author");
        if (string.IsNullOrEmpty(author))
            author = Regex.Match(html, @"name=""author""\s+content=""([^""]*)""", RegexOptions.IgnoreCase).Groups[1].Value;

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = string.IsNullOrEmpty(description) ? null : description,
            Author = string.IsNullOrEmpty(author) ? null : author.Replace(" - YouTube", "").Trim(),
            CoverUrl = coverUrl,
            Platform = Platform.youtube,
            PlatformContentId = videoId,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    private static string? ExtractVideoId(string url)
    {
        // watch?v=ID
        var match = Regex.Match(url, @"[?&]v=([a-zA-Z0-9_-]{11})", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        // youtu.be/ID
        match = Regex.Match(url, @"youtu\.be/([a-zA-Z0-9_-]{11})", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        // shorts/ID
        match = Regex.Match(url, @"shorts/([a-zA-Z0-9_-]{11})", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        return null;
    }

    private static string? ExtractJson(string html, string key)
    {
        var patterns = new[] { $"var {key} = ", $"{key} = " };
        foreach (var pattern in patterns)
        {
            var idx = html.IndexOf(pattern, StringComparison.Ordinal);
            if (idx < 0) continue;

            var start = idx + pattern.Length;
            var braceCount = 0;
            var foundOpening = false;
            var end = start;

            for (var i = start; i < html.Length; i++)
            {
                if (html[i] == '{') { braceCount++; foundOpening = true; }
                else if (html[i] == '}') braceCount--;

                if (foundOpening && braceCount == 0) { end = i + 1; break; }
            }

            if (foundOpening && braceCount == 0)
                return html[start..end];
        }
        return null;
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
