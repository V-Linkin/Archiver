using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Models;

/// <summary>
/// 回收站记录 — 对应 SQLite 表 trash_records
/// </summary>
public class TrashRecord
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public DateTimeOffset DeletedAt { get; set; }
    public DateTimeOffset AutoDeleteAt { get; set; }
    public Guid? OriginalFolderId { get; set; }
    public ArchiveStatus OriginalArchiveStatus { get; set; }
    public List<string> MediaPaths { get; set; } = new();
}
