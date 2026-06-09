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
    public string CountDisplay => $"{Count} 条内容";
}
