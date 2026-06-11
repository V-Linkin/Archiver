using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Import;

/// <summary>
/// 导入结果
/// </summary>
public class ImportResult
{
    public ImportStatus Status { get; init; }
    public string Message { get; init; } = "";
    public string? OriginalInput { get; init; }
    public string? ExtractedUrl { get; init; }
    public string? NormalizedUrl { get; init; }
    public Platform? Platform { get; init; }
    public string? PlatformDisplayName { get; init; }
    public string? PlatformContentId { get; init; }
    public Guid? ImportTaskId { get; init; }
    public string? ErrorMessage { get; init; }

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

    public static ImportResult UnsupportedPlatform(string url) => new()
    {
        Status = ImportStatus.UnsupportedPlatform,
        Message = "已识别为 URL，但暂不支持该平台。",
        ExtractedUrl = url
    };

    public static ImportResult Duplicate(string url, string message) => new()
    {
        Status = ImportStatus.Duplicate,
        Message = message,
        ExtractedUrl = url
    };

    public static ImportResult TaskCreated(Guid taskId, string url, Platform platform, string? contentId) => new()
    {
        Status = ImportStatus.TaskCreated,
        Message = $"识别到平台：{platform.GetDisplayName()}。已创建导入任务，解析器将在后续阶段支持。",
        ExtractedUrl = url,
        Platform = platform,
        PlatformDisplayName = platform.GetDisplayName(),
        PlatformContentId = contentId,
        ImportTaskId = taskId
    };

    public static ImportResult ParserNotImplemented(string url, Platform platform) => new()
    {
        Status = ImportStatus.ParserNotImplemented,
        Message = $"识别到平台：{platform.GetDisplayName()}。该平台解析器将在后续 Phase 7D 支持。",
        ExtractedUrl = url,
        Platform = platform,
        PlatformDisplayName = platform.GetDisplayName()
    };

    public static ImportResult Failed(string url, string error) => new()
    {
        Status = ImportStatus.Failed,
        Message = $"导入失败：{error}",
        ExtractedUrl = url,
        ErrorMessage = error
    };

    public static ImportResult SuccessImport(Guid itemId, string url, Platform platform, string title) => new()
    {
        Status = ImportStatus.SuccessImport,
        Message = $"已导入 {platform.GetDisplayName()} 内容：{title}",
        ExtractedUrl = url,
        Platform = platform,
        PlatformDisplayName = platform.GetDisplayName(),
        ImportTaskId = itemId
    };
}
