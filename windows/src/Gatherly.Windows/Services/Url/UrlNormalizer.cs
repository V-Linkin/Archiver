using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Url;

/// <summary>
/// URL 标准化器 — 从 shared/url 迁移
/// 用于：识别平台、标准化 URL、提取内容 ID
/// </summary>
public static partial class UrlNormalizer
{
    /// <summary>
    /// 从混合文本中提取所有支持平台的 URL
    /// </summary>
    public static List<string> ExtractUrls(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var results = new List<string>();
        foreach (Match match in UrlDetector().Matches(text))
        {
            var url = match.Value;
            if (RecognizePlatform(url).HasValue)
                results.Add(url);
        }
        return results;
    }

    /// <summary>
    /// 从混合文本中提取第一个支持的 URL
    /// </summary>
    public static string? ExtractFirstUrl(string text) => ExtractUrls(text).FirstOrDefault();

    /// <summary>
    /// 验证 URL 是否合法（scheme 为 http/https，或看起来像域名）
    /// </summary>
    public static bool IsValidUrl(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
            return uri.Scheme is "http" or "https";
        if (!input.Contains('.') || input.Contains(' ') || input.StartsWith('-'))
            return false;
        return Uri.TryCreate("https://" + input, UriKind.Absolute, out _);
    }

    /// <summary>
    /// 识别 URL 所属平台
    /// </summary>
    public static Platform? RecognizePlatform(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var lower = url.ToLowerInvariant();

        if (lower.Contains("douyin.com") || lower.Contains("iesdouyin.com"))
            return Platform.douyin;
        if (lower.Contains("xiaohongshu.com") || lower.Contains("xhslink.com"))
            return Platform.xiaohongshu;
        if (lower.Contains("coolapk.com") || lower.Contains("coolapk1s.com"))
            return Platform.coolapk;
        if (lower.Contains("bilibili.com") || lower.Contains("b23.tv"))
            return Platform.bilibili;
        if (lower.Contains("github.com"))
            return Platform.github;
        if (lower.Contains("youtube.com") || lower.Contains("youtu.be"))
            return Platform.youtube;
        if (lower.Contains("x.com") || lower.Contains("twitter.com"))
            return Platform.x;
        if (lower.Contains("weibo.com") || lower.Contains("m.weibo.cn"))
            return Platform.weibo;
        if (lower.Contains("zhihu.com"))
            return Platform.zhihu;
        if (lower.Contains("douban.com"))
            return Platform.douban;

        return null;
    }

    /// <summary>
    /// 标准化 URL（自定义 scheme，只含 ID）
    /// </summary>
    public static string Normalize(string url, Platform platform)
    {
        var id = ExtractContentId(url, platform);
        return id != null
            ? GetCanonicalTemplate(platform).Replace("{id}", id)
            : url;
    }

    /// <summary>
    /// 提取平台内容 ID
    /// </summary>
    public static string? ExtractContentId(string url, Platform platform) => platform switch
    {
        Platform.douyin => ExtractFirstGroup(url, DouyinIdRegex()),
        Platform.xiaohongshu => ExtractFirstGroup(url, XiaohongshuIdRegex()),
        Platform.coolapk => ExtractFirstGroup(url, CoolapkIdRegex()),
        Platform.bilibili => ExtractFirstGroup(url, BilibiliIdRegex()),
        Platform.github => ExtractFirstGroup(url, GitHubIdRegex()),
        Platform.youtube => ExtractFirstGroup(url, YouTubeIdRegex()),
        Platform.x => ExtractFirstGroup(url, XIdRegex()),
        Platform.weibo => ExtractFirstGroup(url, WeiboIdRegex()),
        Platform.zhihu => ExtractFirstGroup(url, ZhihuIdRegex()),
        Platform.douban => ExtractFirstGroup(url, DoubanIdRegex()),
        _ => null
    };

    /// <summary>
    /// 提取 YouTube video/channel/handle ID
    /// </summary>
    public static string? ExtractYouTubeId(string url) =>
        ExtractFirstGroup(url, YouTubeIdRegex());

    /// <summary>
    /// 提取 Bilibili BV/av ID
    /// </summary>
    public static string? ExtractBilibiliBV(string url) =>
        ExtractFirstGroup(url, BilibiliIdRegex());

    /// <summary>
    /// 提取 X tweet ID
    /// </summary>
    public static string? ExtractXId(string url) =>
        ExtractFirstGroup(url, XIdRegex());

    /// <summary>
    /// 提取 X username
    /// </summary>
    public static string? ExtractXUsername(string url) =>
        ExtractFirstGroup(url, XUsernameRegex());

    private static string GetCanonicalTemplate(Platform platform) => platform switch
    {
        Platform.douyin => "douyin://video/{id}",
        Platform.xiaohongshu => "xiaohongshu://explore/{id}",
        Platform.coolapk => "coolapk://feed/{id}",
        Platform.bilibili => "bilibili://video/{id}",
        Platform.github => "github://repo/{id}",
        Platform.youtube => "youtube://video/{id}",
        Platform.x => "x://tweet/{id}",
        Platform.weibo => "weibo://status/{id}",
        Platform.zhihu => "zhihu://content/{id}",
        Platform.douban => "douban://subject/{id}",
        _ => "{url}"
    };

    private static string? ExtractFirstGroup(string input, Regex regex)
    {
        var match = regex.Match(input);
        return match.Success && match.Groups.Count > 1
            ? match.Groups[1].Value
            : null;
    }

    private static string? ExtractFirstGroup(string input, Regex[] regexes)
    {
        foreach (var regex in regexes)
        {
            var match = regex.Match(input);
            if (match.Success && match.Groups.Count > 1)
                return match.Groups[1].Value;
        }
        return null;
    }

    [GeneratedRegex("""https?://[^\s<>"']+""", RegexOptions.IgnoreCase)]
    private static partial Regex UrlDetector();

    [GeneratedRegex("""douyin\.com/video/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex DouyinRegex1();
    [GeneratedRegex("""iesdouyin\.com/share/video/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex DouyinRegex2();
    private static Regex[] DouyinIdRegex() => [DouyinRegex1(), DouyinRegex2()];

    [GeneratedRegex("""xiaohongshu\.com/explore/([a-f0-9]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex XiaohongshuRegex1();
    [GeneratedRegex("""xiaohongshu\.com/discovery/item/([a-f0-9]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex XiaohongshuRegex2();
    private static Regex[] XiaohongshuIdRegex() => [XiaohongshuRegex1(), XiaohongshuRegex2()];

    [GeneratedRegex("""coolapk\.com/feed/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CoolapkRegex1();
    [GeneratedRegex("""coolapk1s\.com/feed/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex CoolapkRegex2();
    private static Regex[] CoolapkIdRegex() => [CoolapkRegex1(), CoolapkRegex2()];

    [GeneratedRegex("""bilibili\.com/video/(BV[a-zA-Z0-9]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex BilibiliRegex1();
    [GeneratedRegex("""bilibili\.com/video/(av\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex BilibiliRegex2();
    private static Regex[] BilibiliIdRegex() => [BilibiliRegex1(), BilibiliRegex2()];

    [GeneratedRegex("""github\.com/([a-zA-Z0-9._\-]+/[a-zA-Z0-9._\-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubRegex();
    private static Regex[] GitHubIdRegex() => [GitHubRegex()];

    [GeneratedRegex("""youtube\.com/watch\?v=([a-zA-Z0-9_-]{11})""", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeRegex1();
    [GeneratedRegex("""youtu\.be/([a-zA-Z0-9_-]{11})""", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeRegex2();
    [GeneratedRegex("""youtube\.com/embed/([a-zA-Z0-9_-]{11})""", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeRegex3();
    [GeneratedRegex("""youtube\.com/shorts/([a-zA-Z0-9_-]{11})""", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeRegex4();
    [GeneratedRegex("""youtube\.com/channel/([a-zA-Z0-9_-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeRegex5();
    [GeneratedRegex("""youtube\.com/(@[a-zA-Z0-9._-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex YouTubeRegex6();
    private static Regex[] YouTubeIdRegex() => [
        YouTubeRegex1(), YouTubeRegex2(), YouTubeRegex3(),
        YouTubeRegex4(), YouTubeRegex5(), YouTubeRegex6()
    ];

    [GeneratedRegex("""(?:x|twitter)\.com/[^/]+/status/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex XRegex1();
    [GeneratedRegex("""(?:x|twitter)\.com/i/status/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex XRegex2();
    private static Regex[] XIdRegex() => [XRegex1(), XRegex2()];

    [GeneratedRegex("""(?:x|twitter)\.com/([a-zA-Z0-9_]+)/status/""", RegexOptions.IgnoreCase)]
    private static partial Regex XUsernameRegex1();
    [GeneratedRegex("""(?:x|twitter)\.com/([a-zA-Z0-9_]+)$""", RegexOptions.IgnoreCase)]
    private static partial Regex XUsernameRegex2();
    private static Regex[] XUsernameRegex() => [XUsernameRegex1(), XUsernameRegex2()];

    [GeneratedRegex("""weibo\.com/status/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex WeiboRegex1();
    [GeneratedRegex("""weibo\.com/\d+/([a-zA-Z0-9]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex WeiboRegex2();
    [GeneratedRegex("""m\.weibo\.cn/detail/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex WeiboRegex3();
    [GeneratedRegex("""m\.weibo\.cn/status/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex WeiboRegex4();
    private static Regex[] WeiboIdRegex() => [
        WeiboRegex1(), WeiboRegex2(), WeiboRegex3(), WeiboRegex4()
    ];

    [GeneratedRegex("""zhihu\.com/question/\d+/answer/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ZhihuRegex1();
    [GeneratedRegex("""zhihu\.com/p/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ZhihuRegex2();
    [GeneratedRegex("""zhihu\.com/column/([a-zA-Z0-9_-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ZhihuRegex3();
    private static Regex[] ZhihuIdRegex() => [ZhihuRegex1(), ZhihuRegex2(), ZhihuRegex3()];

    [GeneratedRegex("""douban\.com/subject/(\d+)""", RegexOptions.IgnoreCase)]
    private static partial Regex DoubanRegex();
    private static Regex[] DoubanIdRegex() => [DoubanRegex()];
}
