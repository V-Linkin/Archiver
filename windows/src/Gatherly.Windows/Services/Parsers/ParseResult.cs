using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Parsers;

/// <summary>
/// 解析结果状态
/// </summary>
public enum ParseStatus
{
    Success,
    NotImplemented,
    UnsupportedPlatform,
    Failed
}

/// <summary>
/// 解析结果
/// </summary>
public class ParseResult
{
    public ParseStatus Status { get; init; }
    public ParsedContent? Content { get; init; }
    public string? ErrorMessage { get; init; }

    public static ParseResult NotImpl => new() { Status = ParseStatus.NotImplemented };
    public static ParseResult Unsupported => new() { Status = ParseStatus.UnsupportedPlatform };

    public static ParseResult Success(ParsedContent content) => new()
    {
        Status = ParseStatus.Success,
        Content = content
    };

    public static ParseResult Fail(string error) => new()
    {
        Status = ParseStatus.Failed,
        ErrorMessage = error
    };
}

/// <summary>
/// 解析后的完整内容
/// </summary>
public class ParsedContent
{
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? Author { get; init; }
    public string? AuthorId { get; init; }
    public DateTimeOffset? PublishDate { get; init; }
    public string? CoverUrl { get; init; }
    public List<string> ImageUrls { get; init; } = [];
    public string? VideoUrl { get; init; }
    public string? PlatformContentId { get; init; }
    public string? OriginalUrl { get; init; }
    public string? NormalizedUrl { get; init; }
    public Platform Platform { get; init; }
    public Dictionary<string, string> RawMetadata { get; init; } = [];
}

/// <summary>
/// 解析请求
/// </summary>
public class ParseRequest
{
    public string Url { get; init; } = "";
    public string NormalizedUrl { get; init; } = "";
    public Platform Platform { get; init; }
    public string? PlatformContentId { get; init; }
}
