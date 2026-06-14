using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Import;
using Gatherly.Windows.Services.Media;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services;

/// <summary>
/// 导入服务 — Phase 7D-3: 支持 YouTube 写入 item + 下载封面
/// URL 提取 → 平台识别 → 去重检查 → 创建任务 → Router → Parser → 写入 item → 下载封面
/// </summary>
public class ImportService
{
    private readonly ItemRepository _itemRepo;
    private readonly ImportTaskRepository _taskRepo;
    private readonly MediaDownloadService _mediaDownload;
    private readonly PlatformRouter _router;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 活跃任务窗口：pending/importing 任务在此时间内视为活跃
    /// </summary>
    private static readonly TimeSpan ActiveTaskWindow = TimeSpan.FromMinutes(10);

    public ImportService(ItemRepository itemRepo, ImportTaskRepository taskRepo, MediaDownloadService mediaDownload)
        : this(itemRepo, taskRepo, mediaDownload, TimeProvider.System)
    {
    }

    public ImportService(ItemRepository itemRepo, ImportTaskRepository taskRepo, MediaDownloadService mediaDownload, TimeProvider timeProvider)
    {
        _itemRepo = itemRepo;
        _taskRepo = taskRepo;
        _mediaDownload = mediaDownload;
        _router = new PlatformRouter();
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// 处理导入请求
    /// </summary>
    public async Task<ImportResult> ProcessImportAsync(string? input)
    {
        // 1. 空输入
        if (string.IsNullOrWhiteSpace(input))
            return ImportResult.EmptyInput;

        var trimmed = input.Trim();

        // 2. 提取 URL
        var url = UrlNormalizer.ExtractFirstUrl(trimmed);
        if (url == null)
        {
            if (UrlNormalizer.IsValidUrl(trimmed))
                url = trimmed;
            else
                return ImportResult.InvalidUrl;
        }

        // 3. 识别平台
        var platform = UrlNormalizer.RecognizePlatform(url);
        if (!platform.HasValue)
            return ImportResult.UnsupportedPlatform(url);

        // 4. Normalize + 提取 ID
        var normalizedUrl = UrlNormalizer.Normalize(url, platform.Value);
        var contentId = UrlNormalizer.ExtractContentId(url, platform.Value);

        // 5. Duplicate 检查 — items（仅检查活跃 item，回收站中的同 URL 不阻止导入）
        var existingItem = await _itemRepo.GetByNormalizedUrlAsync(normalizedUrl);
        if (existingItem != null)
        {
            return ImportResult.DuplicateExistingItem(url);
        }

        // 5b. Duplicate 检查 — import_tasks
        var existingTasks = await _taskRepo.GetAllByNormalizedUrlAsync(normalizedUrl);
        if (existingTasks.Count > 0)
        {
            // Check if any task has a valid active item
            foreach (var t in existingTasks)
            {
                if (t.Status == Models.Enums.TaskStatus.completed && t.ItemId.HasValue)
                {
                    var taskItem = await _itemRepo.GetByIdAsync(t.ItemId.Value);
                    if (taskItem != null && taskItem.DeletedAt == null)
                    {
                        return ImportResult.DuplicateExistingItem(url, taskItem.Id);
                    }
                    // task points to trashed item → skip, allow re-import
                }
            }

            // Check if any task is recent and active (pending without error)
            var utcNow = _timeProvider.GetUtcNow();
            foreach (var t in existingTasks)
            {
                if (t.Status == Models.Enums.TaskStatus.pending && string.IsNullOrEmpty(t.ErrorMessage))
                {
                    var taskAge = utcNow - t.UpdatedAt;
                    if (taskAge <= ActiveTaskWindow)
                    {
                        return ImportResult.DuplicateImportTask(url);
                    }
                }
            }

            // All tasks are stale, failed, or orphan → allow re-import
        }

        // 6. 创建 import_task
        var now = _timeProvider.GetUtcNow();
        var task = new ImportTask
        {
            Id = Guid.NewGuid(),
            OriginalUrl = url,
            NormalizedUrl = normalizedUrl,
            Platform = platform.Value,
            Status = Models.Enums.TaskStatus.pending,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _taskRepo.InsertAsync(task);

        // 7. Router → Parser
        var parser = _router.GetParser(platform.Value, url);
        var parseResult = await parser.ParseAsync(new ParseRequest
        {
            Url = url,
            NormalizedUrl = normalizedUrl,
            Platform = platform.Value,
            PlatformContentId = contentId
        });

        // 8. 处理解析结果
        if (parseResult.Status == ParseStatus.NotImplemented)
        {
            await _taskRepo.UpdateStatusAsync(task.Id, Models.Enums.TaskStatus.pending, "解析器尚未实现");
            return ImportResult.TaskCreated(task.Id, url, platform.Value, contentId);
        }

        if (parseResult.Status == ParseStatus.Failed)
        {
            await _taskRepo.UpdateStatusAsync(task.Id, Models.Enums.TaskStatus.failed, parseResult.ErrorMessage);
            return ImportResult.Failed(url, parseResult.ErrorMessage ?? "未知错误");
        }

        // 9. Success: 创建新 item（标准流程，始终使用新 ID）
        if (parseResult.Status == ParseStatus.Success && parseResult.Content != null)
        {
            var content = parseResult.Content;
            var item = new Item
            {
                Id = Guid.NewGuid(),
                Title = content.Title,
                Body = content.Body,
                OriginalUrl = content.OriginalUrl ?? url,
                Platform = platform.Value,
                PlatformContentId = content.PlatformContentId ?? contentId,
                NormalizedUrl = content.NormalizedUrl ?? normalizedUrl,
                Author = content.Author,
                AuthorId = content.AuthorId,
                PublishDate = content.PublishDate,
                ImportDate = DateTimeOffset.UtcNow,
                ModifyDate = DateTimeOffset.UtcNow,
                ContentStatus = ContentStatus.normal,
                ArchiveStatus = ArchiveStatus.pending,
                MediaStatus = string.IsNullOrEmpty(content.VideoUrl)
                    ? (content.ImageUrls.Count > 0 ? MediaStatus.complete : MediaStatus.textOnly)
                    : MediaStatus.complete,
                CoverUrl = content.CoverUrl
            };

            await _itemRepo.InsertAsync(item);

            // 下载封面到本地（失败不影响导入）
            if (!string.IsNullOrEmpty(content.CoverUrl))
            {
                await _mediaDownload.DownloadCoverAsync(item.Id, content.CoverUrl);
            }

            await _taskRepo.UpdateCompletedAsync(task.Id, item.Id);

            return ImportResult.SuccessImport(item.Id, url, platform.Value, content.Title ?? $"{content.Author}/{content.PlatformContentId}");
        }

        // Fallback
        await _taskRepo.UpdateStatusAsync(task.Id, Models.Enums.TaskStatus.completed);
        return ImportResult.TaskCreated(task.Id, url, platform.Value, contentId);
    }
}
