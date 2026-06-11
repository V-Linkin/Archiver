using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services;

/// <summary>
/// 导入结果状态
/// </summary>
public enum ImportStatus
{
    EmptyInput,
    InvalidUrl,
    UnsupportedPlatform,
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
    public Platform? DetectedPlatform { get; init; }

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

    public static ImportResult PlatformRecognized(string url, Platform platform) => new()
    {
        Status = ImportStatus.UnsupportedPendingParser,
        Message = $"识别到平台：{platform.GetDisplayName()}。Windows 原生解析将在后续 Phase 7C/7D 支持。",
        DetectedUrl = url,
        DetectedPlatform = platform
    };

    public static ImportResult UnknownPlatform(string url) => new()
    {
        Status = ImportStatus.UnsupportedPlatform,
        Message = "已识别为 URL，但暂不支持该平台。",
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
/// 导入服务 — Phase 7B: 接入 UrlNormalizer
/// 从用户输入提取 URL、识别平台，但不抓取内容
/// </summary>
public class ImportService
{
    /// <summary>
    /// 处理导入请求
    /// </summary>
    public ImportResult ProcessImport(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ImportResult.EmptyInput;

        var trimmed = input.Trim();

        var url = UrlNormalizer.ExtractFirstUrl(trimmed);
        if (url == null)
        {
            if (UrlNormalizer.IsValidUrl(trimmed))
                url = trimmed;
            else
                return ImportResult.InvalidUrl;
        }

        var platform = UrlNormalizer.RecognizePlatform(url);
        if (platform.HasValue)
            return ImportResult.PlatformRecognized(url, platform.Value);

        return ImportResult.UnknownPlatform(url);
    }
}
