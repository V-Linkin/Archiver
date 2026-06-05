using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Models;

/// <summary>
/// 文件夹 — 对应 SQLite 表 folders
/// </summary>
public class Folder
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Platform Platform { get; set; }
    public Guid? CustomPlatformId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int SortOrder { get; set; }
}
