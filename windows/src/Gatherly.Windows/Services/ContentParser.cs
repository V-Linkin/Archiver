using System.Text.RegularExpressions;

namespace Gatherly.Windows.Services;

/// <summary>
/// 正文解析器 — 提取 URL 并生成内容片段
/// </summary>
public static partial class ContentParser
{
    /// <summary>
    /// 匹配 http/https URL
    /// </summary>
    [GeneratedRegex(@"https?://[^\s<>\)\]\}，。；：！？、""''）】》》]+")]
    private static partial Regex UrlRegex();

    /// <summary>
    /// 匹配 URL 尾部标点（需要排除）
    /// </summary>
    private static readonly HashSet<char> TrailingPunctuation = new()
    {
        '.', ',', ';', ':', '!', '?',
        '\u3002', '\uFF0C', '\uFF1B', '\uFF1A', '\uFF01', '\uFF1F', // 。 ， ； ： ！ ？
        '\u3001', '\u201C', '\u2018', '\uFF09', '\u3011', '\u300B', ']', ')', '}' // 、 " ' ） 】 》
    };

    /// <summary>
    /// 将正文文本解析为内容片段
    /// </summary>
    public static IReadOnlyList<ContentSegment> ParseSegments(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var segments = new List<ContentSegment>();
        var matches = UrlRegex().Matches(text);

        if (matches.Count == 0)
        {
            segments.Add(new ContentSegment(text, false));
            return segments;
        }

        var lastIndex = 0;

        foreach (Match match in matches)
        {
            // 添加 URL 之前的普通文本
            if (match.Index > lastIndex)
            {
                var beforeText = text[lastIndex..match.Index];
                if (beforeText.Length > 0)
                    segments.Add(new ContentSegment(beforeText, false));
            }

            // 处理 URL，排除尾随标点
            var url = match.Value;
            while (url.Length > 0 && TrailingPunctuation.Contains(url[^1]))
            {
                url = url[..^1];
            }

            if (url.Length > 0)
            {
                segments.Add(new ContentSegment(url, true, url));
            }

            lastIndex = match.Index + match.Length;
        }

        // 添加最后一个 URL 之后的普通文本
        if (lastIndex < text.Length)
        {
            var afterText = text[lastIndex..];
            if (afterText.Length > 0)
                segments.Add(new ContentSegment(afterText, false));
        }

        return segments;
    }
}
