using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Models;

/// <summary>
/// 回收站记录 — 对应 SQLite 表 trash_records
/// </summary>
public class TrashRecord
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }

    /// <summary>
    /// 非数据库字段：数据库中的原始 item_id 字符串（保留大小写，用于 FK 匹配）
    /// </summary>
    public string? RawItemId { get; set; }

    public DateTimeOffset DeletedAt { get; set; }
    public DateTimeOffset AutoDeleteAt { get; set; }
    public Guid? OriginalFolderId { get; set; }
    public ArchiveStatus OriginalArchiveStatus { get; set; }
    public List<string> MediaPaths { get; set; } = new();
}
