using System.Text.RegularExpressions;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// GitHub Parser — 支持 repo / issue / pull / discussion 页面
/// 使用 HTTP GET + HTML meta 解析，不依赖 GitHub API token
/// </summary>
public partial class GitHubParser : IContentParser
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders =
        {
            { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" }
        }
    };

    public bool CanParse(Platform platform, string url) =>
        platform == Platform.github;

    public async Task<ParseResult> ParseAsync(ParseRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryExtractOwnerRepo(request.Url, out var owner, out var repo, out var pathType, out var number))
            return ParseResult.Fail("无法从 URL 提取仓库信息");

        try
        {
            var html = await SharedHttpClient.GetStringAsync(request.Url, cancellationToken);

            var title = ExtractMeta(html, "og:title")
                .Replace("GitHub - ", "")
                .Split(':').FirstOrDefault()?.Trim()
                ?? $"{owner}/{repo}";

            var description = ExtractMeta(html, "og:description")
                .Split(". Contribute to").FirstOrDefault()
                ?? ExtractMeta(html, "og:description")
                ?? "";

            var coverUrl = ExtractMeta(html, "og:image");
            var stars = ExtractPattern(html, """(\d[\d,]*)\s*stargazers?""");
            var forks = ExtractPattern(html, """(\d[\d,]*)\s*forks?""");
            var language = ExtractPattern(html, """itemprop="programmingLanguage">([^<]*)<""");

            var bodyParts = new List<string>();
            if (!string.IsNullOrEmpty(description))
                bodyParts.Add(description);
            if (!string.IsNullOrEmpty(stars) || !string.IsNullOrEmpty(forks))
                bodyParts.Add($"⭐ {stars ?? "0"}  🍴 {forks ?? "0"}");
            if (!string.IsNullOrEmpty(language))
                bodyParts.Add($"语言: {language}");

            var platformContentId = pathType switch
            {
                "issues" => $"{owner}/{repo}/issues/{number}",
                "pull" => $"{owner}/{repo}/pull/{number}",
                "discussions" => $"{owner}/{repo}/discussions/{number}",
                _ => $"{owner}/{repo}"
            };

            return ParseResult.Success(new ParsedContent
            {
                Title = title,
                Body = string.Join("\n\n", bodyParts),
                Author = owner,
                AuthorId = owner,
                CoverUrl = coverUrl,
                Platform = Platform.github,
                PlatformContentId = platformContentId,
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

    private static bool TryExtractOwnerRepo(string url, out string owner, out string repo, out string pathType, out string? number)
    {
        owner = "";
        repo = "";
        pathType = "repo";
        number = null;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Host != "github.com")
            return false;

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
            return false;

        owner = segments[0];
        repo = segments[1];

        if (segments.Length >= 3)
        {
            pathType = segments[2].ToLowerInvariant();
            if (segments.Length >= 4)
                number = segments[3];
        }

        var skip = new[] { "topics", "collections", "organizations", "settings", "notifications", "search", "login", "signup", "explore", "trending", "stars", "watching" };
        if (skip.Contains(repo, StringComparer.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static string ExtractMeta(string html, string property)
    {
        var pattern = $@"{property}""\s+content=""([^""]*)""";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string? ExtractPattern(string html, string pattern)
    {
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }
}
