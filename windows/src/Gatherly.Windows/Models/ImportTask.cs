using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Models;

/// <summary>
/// 导入任务 — 对应 SQLite 表 import_tasks
/// </summary>
public class ImportTask
{
    public Guid Id { get; set; }
    public string OriginalUrl { get; set; } = string.Empty;
    public string NormalizedUrl { get; set; } = string.Empty;
    public Platform? Platform { get; set; }
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.pending;
    public double Progress { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? ItemId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int RetryCount { get; set; }
}
