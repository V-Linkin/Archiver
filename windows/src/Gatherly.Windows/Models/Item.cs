using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Models;

/// <summary>
/// 内容主体 — 对应 SQLite 表 items
/// </summary>
public class Item
{
    public Guid Id { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public Platform Platform { get; set; }
    public string? PlatformContentId { get; set; }
    public string NormalizedUrl { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? AuthorId { get; set; }
    public DateTimeOffset? PublishDate { get; set; }
    public DateTimeOffset ImportDate { get; set; }
    public DateTimeOffset ModifyDate { get; set; }
    public ContentStatus ContentStatus { get; set; } = ContentStatus.normal;
    public ArchiveStatus ArchiveStatus { get; set; } = ArchiveStatus.pending;
    public MediaStatus MediaStatus { get; set; } = MediaStatus.textOnly;
    public Guid? CoverAssetId { get; set; }
    public Guid? FolderId { get; set; }
    public string? Remark { get; set; }
    public bool IsStarred { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? CustomPlatformId { get; set; }
}
