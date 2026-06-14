using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 平台入口展示模型
/// </summary>
public class PlatformEntryDisplay
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public int Count { get; set; }
    public string? LogoPath { get; set; }
    public bool IsUncategorized { get; set; }
    public bool IsStandardPlatform { get; set; }
    public Platform? StandardPlatform { get; set; }
    public List<Guid> CustomPlatformIds { get; set; } = new();
    public string CountDisplay => $"{Count} 条内容";
}
