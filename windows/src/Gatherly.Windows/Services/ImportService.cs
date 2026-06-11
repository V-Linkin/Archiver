namespace Gatherly.Windows.Services;

/// <summary>
/// 导入结果状态
/// </summary>
public enum ImportStatus
{
    EmptyInput,
    InvalidUrl,
    UnsupportedPendingParser,
    DuplicateUrl
}

/// <summary>
/// 导入结果
/// </summary>
public class ImportResult
{
    public ImportStatus Status { get; init; }
    public string Message { get; init; } = "";
    public string? DetectedUrl { get; init; }

    public static ImportResult EmptyInput => new()
    {
        Status = ImportStatus.EmptyInput,
        Message = "请输入或粘贴链接"
    };

    public static ImportResult InvalidUrl => new()
    {
        Status = ImportStatus.InvalidUrl,
        Message = "输入的内容不是有效的 URL"
    };

    public static ImportResult UnsupportedPendingParser(string url) => new()
    {
        Status = ImportStatus.UnsupportedPendingParser,
        Message = "该链接已识别，Windows 原生解析将在后续 Phase 7B/7C 支持。",
        DetectedUrl = url
    };

    public static ImportResult DuplicateUrl(string url) => new()
    {
        Status = ImportStatus.DuplicateUrl,
        Message = "该链接已存在于归档库中",
        DetectedUrl = url
    };
}

/// <summary>
/// 导入服务骨架 — Phase 7A
/// 当前只做输入验证和 URL 检测，不创建真实 item
/// 后续 Phase 7B/7C 迁移 URLNormalizer + Parser
/// </summary>
public class ImportService
{
    /// <summary>
    /// 处理导入请求
    /// </summary>
    public ImportResult ProcessImport(string? input)
    {
        // 1. 空输入
        if (string.IsNullOrWhiteSpace(input))
            return ImportResult.EmptyInput;

        var trimmed = input.Trim();

        // 2. 尝试提取 URL
        var url = ExtractUrl(trimmed);
        if (url == null)
            return ImportResult.InvalidUrl;

        // 3. URL 已识别，但 Parser 尚未实现
        return ImportResult.UnsupportedPendingParser(url);
    }

    /// <summary>
    /// 从输入文本中提取第一个 URL
    /// </summary>
    private static string? ExtractUrl(string text)
    {
        try
        {
            var detector = new System.Text.RegularExpressions.Regex(
                @"https?://[^\s]+",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var match = detector.Match(text);
            return match.Success ? match.Value : null;
        }
        catch
        {
            return null;
        }
    }
}
