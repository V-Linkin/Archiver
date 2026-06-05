namespace Gatherly.Windows.Models;

/// <summary>
/// 自定义平台 — 对应 SQLite 表 custom_platforms
/// </summary>
public class CustomPlatform
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LogoPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int SortOrder { get; set; }
}
