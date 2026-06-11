using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services.Import;

/// <summary>
/// 导入请求
/// </summary>
public class ImportRequest
{
    public string OriginalInput { get; init; } = "";
    public string ExtractedUrl { get; init; } = "";
    public string NormalizedUrl { get; init; } = "";
    public Platform Platform { get; init; }
    public string? PlatformContentId { get; init; }
}
