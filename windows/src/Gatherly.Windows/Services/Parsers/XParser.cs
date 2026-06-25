using System.Net.Http;
using System.Text.Json;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// X (Twitter) 解析器 — 通过 fxtwitter API 获取推文数据
/// 对齐 macOS XParser.swift
/// </summary>
public class XParser : IContentParser
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1" },
            { "Accept", "application/json" }
        }
    };

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.x;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var tweetId = UrlNormalizer.ExtractXId(request.Url);
            if (string.IsNullOrEmpty(tweetId))
                return ParseResult.Fail("无法从链接提取推文 ID");

            var username = UrlNormalizer.ExtractXUsername(request.Url);
            if (string.IsNullOrEmpty(username))
                return ParseResult.Fail("无法从链接提取用户名，请确保链接包含完整的用户名");

            // 使用 fxtwitter API 获取推文数据（对齐 macOS）
            var apiUrl = $"https://api.fxtwitter.com/{username}/status/{tweetId}";
            var json = await SharedHttpClient.GetStringAsync(apiUrl, cancellationToken);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tweet", out var tweet))
                return ParseResult.Fail("推文数据格式错误");

            return ParseTweetJson(tweet, tweetId, request);
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

    private ParseResult ParseTweetJson(JsonElement tweet, string tweetId, ParseRequest request)
    {
        // 正文
        var fullText = GetString(tweet, "text");

        // 作者
        string? author = null, authorId = null, avatarUrl = null;
        if (tweet.TryGetProperty("author", out var authorData) && authorData.ValueKind == JsonValueKind.Object)
        {
            author = GetString(authorData, "name");
            authorId = GetString(authorData, "screen_name");
            avatarUrl = GetString(authorData, "avatar_url");
        }

        // 发布时间
        DateTimeOffset? publishDate = null;
        if (tweet.TryGetProperty("created_at", out var cat) && cat.ValueKind == JsonValueKind.String)
            publishDate = ParseXDate(cat.GetString()!);
        else if (tweet.TryGetProperty("created_timestamp", out var ts) && ts.TryGetDouble(out var tsVal))
            publishDate = DateTimeOffset.FromUnixTimeSeconds((long)tsVal);

        // 媒体
        var imageUrls = new List<string>();
        string? coverUrl = null, videoUrl = null;

        if (tweet.TryGetProperty("media", out var media) && media.ValueKind == JsonValueKind.Object)
        {
            // 图片
            if (media.TryGetProperty("photos", out var photos) && photos.ValueKind == JsonValueKind.Array)
            {
                foreach (var photo in photos.EnumerateArray())
                    if (photo.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                        imageUrls.Add(urlEl.GetString()!);
            }

            // 视频
            if (media.TryGetProperty("videos", out var videos) && videos.ValueKind == JsonValueKind.Array && videos.GetArrayLength() > 0)
            {
                var firstVideo = videos[0];
                if (firstVideo.TryGetProperty("url", out var vUrl))
                    videoUrl = vUrl.GetString();
                else if (firstVideo.TryGetProperty("preview", out var vPrev))
                    videoUrl = vPrev.GetString();

                if (firstVideo.TryGetProperty("thumbnail_url", out var vThumb))
                    coverUrl = vThumb.GetString();
            }

            // GIF
            if (media.TryGetProperty("gifs", out var gifs) && gifs.ValueKind == JsonValueKind.Array && gifs.GetArrayLength() > 0)
            {
                var firstGif = gifs[0];
                if (firstGif.TryGetProperty("url", out var gUrl))
                    videoUrl = gUrl.GetString();
                if (firstGif.TryGetProperty("thumbnail_url", out var gThumb))
                    coverUrl = gThumb.GetString();
            }
        }

        // quote 中的媒体
        if (imageUrls.Count == 0 && tweet.TryGetProperty("quote", out var quote) && quote.ValueKind == JsonValueKind.Object)
        {
            if (quote.TryGetProperty("media", out var qMedia) && qMedia.TryGetProperty("photos", out var qPhotos) && qPhotos.ValueKind == JsonValueKind.Array)
            {
                foreach (var photo in qPhotos.EnumerateArray())
                    if (photo.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                        imageUrls.Add(urlEl.GetString()!);
            }
        }

        // 封面兜底
        if (string.IsNullOrEmpty(coverUrl))
            coverUrl = imageUrls.Count > 0 ? imageUrls[0] : avatarUrl;

        // 封面去重（对齐 macOS）
        if (!string.IsNullOrEmpty(coverUrl) && imageUrls.Count > 0 && imageUrls[0] == coverUrl)
            imageUrls.RemoveAt(0);

        // 标题取正文前 50 字（对齐 macOS）
        var title = fullText != null && fullText.Length > 50 ? fullText[..50] : fullText;

        // rawMetadata（对齐 macOS）
        var metadata = new Dictionary<string, string>();
        if (tweet.TryGetProperty("likes", out var likes)) metadata["likes"] = likes.ToString();
        if (tweet.TryGetProperty("retweets", out var rts)) metadata["retweets"] = rts.ToString();
        if (tweet.TryGetProperty("replies", out var rep)) metadata["replies"] = rep.ToString();
        if (tweet.TryGetProperty("bookmarks", out var bm)) metadata["bookmarks"] = bm.ToString();
        if (tweet.TryGetProperty("views", out var vw)) metadata["views"] = vw.ToString();
        if (avatarUrl != null) metadata["avatarURL"] = avatarUrl;
        if (authorId != null) metadata["screenName"] = authorId;

        return ParseResult.Success(new ParsedContent
        {
            Title = title,
            Body = fullText,
            Author = author,
            AuthorId = authorId,
            PublishDate = publishDate,
            CoverUrl = coverUrl,
            ImageUrls = imageUrls,
            VideoUrl = videoUrl,
            PlatformContentId = tweetId,
            Platform = Platform.x,
            OriginalUrl = request.Url,
            NormalizedUrl = request.NormalizedUrl
        });
    }

    private static DateTimeOffset? ParseXDate(string dateString)
    {
        if (DateTimeOffset.TryParseExact(dateString, "EEE MMM dd HH:mm:ss zzz yyyy",
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
}
