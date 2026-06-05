using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Models;

/// <summary>
/// 媒体资产 — 对应 SQLite 表 media_assets
/// </summary>
public class MediaAsset
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public MediaType Type { get; set; }
    public string? LocalPath { get; set; }
    public string? RemoteUrl { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? Duration { get; set; }
    public string? Checksum { get; set; }
    public DownloadStatus DownloadStatus { get; set; } = DownloadStatus.pending;
    public string? ThumbnailPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
