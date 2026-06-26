using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 小红书解析器 — HTTP 主路径，对齐 macOS XiaohongshuParser.swift
/// 暂不实现 WebView2 降级
/// </summary>
public partial class XiaohongshuParser : IContentParser
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

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.xiaohongshu;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var html = await SharedHttpClient.GetStringAsync(request.Url, cancellationToken);

            if (!html.Contains("__INITIAL_STATE__="))
                return ParseResult.Fail("页面未包含 __INITIAL_STATE__，可能需要登录");

            var result = ExtractFromSsrData(html, request);
            if (result != null)
                return result;

            return ParseResult.Fail("SSR 数据解析失败");
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
    /// 从 __INITIAL_STATE__ 提取笔记数据（对齐 macOS extractFromSSRData）
    /// </summary>
    private ParseResult? ExtractFromSsrData(string html, ParseRequest request)
    {
        var startMatch = InitialStatePattern().Match(html);
        if (!startMatch.Success) return null;

        var jsonStr = startMatch.Groups[1].Value.Trim();
        if (jsonStr.EndsWith(';'))
            jsonStr = jsonStr[..^1];

        // macOS: replacingOccurrences(of: "undefined", with: "null")
        jsonStr = jsonStr.Replace("undefined", "null");

        try
        {
            var json = JsonDocument.Parse(jsonStr);
            var root = json.RootElement;

            // 小红书两种数据结构：
            // 1. note.noteDetailMap (explore 页面)
            // 2. noteData.data.noteData (discovery/item 页面)
            JsonElement? note = null;

            if (root.TryGetProperty("note", out var noteEl) &&
                noteEl.TryGetProperty("noteDetailMap", out var detailMap) &&
                detailMap.ValueKind == JsonValueKind.Object)
            {
                var firstProp = detailMap.EnumerateObject().FirstOrDefault();
                if (firstProp.Value.ValueKind == JsonValueKind.Object &&
                    firstProp.Value.TryGetProperty("note", out var noteDetail))
                {
                    note = noteDetail;
                }
            }
            else if (root.TryGetProperty("noteData", out var noteDataEl) &&
                     noteDataEl.TryGetProperty("data", out var dataEl) &&
                     dataEl.TryGetProperty("noteData", out var noteDetailEl))
            {
                note = noteDetailEl;
            }

            if (note == null) return null;

            var noteVal = note.Value;

            var title = GetString(noteVal, "title");
            var desc = GetString(noteVal, "desc");

            // 作者
            string? author = null;
            string? authorId = null;
            if (noteVal.TryGetProperty("user", out var userEl) && userEl.ValueKind == JsonValueKind.Object)
            {
                author = GetString(userEl, "nickName") ?? GetString(userEl, "nickname");
                authorId = GetString(userEl, "userId") ?? GetString(userEl, "userid");
            }

            // 图片列表（对齐 macOS 无水印逻辑）
            var imageUrls = new List<string>();
            if (noteVal.TryGetProperty("imageList", out var imageList) && imageList.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in imageList.EnumerateArray())
                {
                    // 优先 fileId 构造无水印 URL
                    var fileId = GetString(img, "fileId");
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        imageUrls.Add($"http://sns-na-i1.xhscdn.com/{fileId}?imageView2/2/w/1080/format/jpg");
                    }
                    else
                    {
                        var urlDefault = GetString(img, "urlDefault") ?? GetString(img, "url");
                        if (!string.IsNullOrEmpty(urlDefault))
                            imageUrls.Add(urlDefault);
                    }
                }
            }

            // 封面（对齐 macOS：优先 normalNotePreloadData，兜底首图）
            string? coverUrl = null;
            if (root.TryGetProperty("noteData", out var ndEl) &&
                ndEl.TryGetProperty("normalNotePreloadData", out var preloadEl) &&
                preloadEl.TryGetProperty("imagesList", out var imagesListEl) &&
                imagesListEl.ValueKind == JsonValueKind.Array &&
                imagesListEl.GetArrayLength() > 0)
            {
                var firstImg = imagesListEl[0];
                coverUrl = GetString(firstImg, "urlSizeLarge") ?? GetString(firstImg, "url");
            }
            if (string.IsNullOrEmpty(coverUrl) && imageUrls.Count > 0)
                coverUrl = imageUrls[0];

            // 封面和首图去重（对齐 macOS：封面一定来自首图，直接移除首图，不比较 URL）
            if (!string.IsNullOrEmpty(coverUrl) && imageUrls.Count > 0)
                imageUrls.RemoveAt(0);

            // 视频（对齐 macOS：优先 h264，选最高分辨率）
            string? videoUrl = null;
            if (noteVal.TryGetProperty("video", out var videoEl) &&
                videoEl.TryGetProperty("media", out var mediaEl) &&
                mediaEl.TryGetProperty("stream", out var streamEl) &&
                streamEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var codec in new[] { "h264", "h265", "av1" })
                {
                    if (streamEl.TryGetProperty(codec, out var codecStreams) && codecStreams.ValueKind == JsonValueKind.Array)
                    {
                        var sorted = codecStreams.EnumerateArray()
                            .OrderByDescending(s => s.TryGetProperty("width", out var w) && w.TryGetInt32(out var width) ? width : 0)
                            .ToList();
                        foreach (var stream in sorted)
                        {
                            if (stream.TryGetProperty("masterUrl", out var masterUrl) &&
                                masterUrl.ValueKind == JsonValueKind.String)
                            {
                                var url = masterUrl.GetString();
                                if (!string.IsNullOrEmpty(url))
                                {
                                    videoUrl = url;
                                    break;
                                }
                            }
                        }
                        if (videoUrl != null) break;
                    }
                }
            }

            if (title == null && desc == null) return null;

            return ParseResult.Success(new ParsedContent
            {
                Title = title,
                Body = desc,
                Author = author,
                AuthorId = authorId,
                CoverUrl = coverUrl,
                ImageUrls = imageUrls,
                VideoUrl = videoUrl,
                PlatformContentId = UrlNormalizer.ExtractContentId(request.Url, Platform.xiaohongshu),
                Platform = Platform.xiaohongshu,
                OriginalUrl = request.Url,
                NormalizedUrl = request.NormalizedUrl
            });
        }
        catch
        {
            return null;
        }
    }

    #region Helpers

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var el))
            return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    #endregion

    #region Regex Patterns

    [GeneratedRegex("""__INITIAL_STATE__\s*=\s*(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex InitialStatePattern();

    public static Match InitialStatePatternForTest(string html) => InitialStatePattern().Match(html);

    #endregion
}
