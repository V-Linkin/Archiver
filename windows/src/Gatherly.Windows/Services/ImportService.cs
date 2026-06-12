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

    public ImportService(ItemRepository itemRepo, ImportTaskRepository taskRepo, MediaDownloadService mediaDownload)
    {
        _itemRepo = itemRepo;
        _taskRepo = taskRepo;
        _mediaDownload = mediaDownload;
        _router = new PlatformRouter();
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

        // 5. Duplicate 检查 — items
        var existingItem = await _itemRepo.GetByNormalizedUrlAsync(normalizedUrl);
        if (existingItem != null)
        {
            // 检查是否在回收站
            var trashedItem = await _itemRepo.GetByIdAsync(existingItem.Id);
            if (trashedItem != null && trashedItem.DeletedAt != null)
                return ImportResult.DuplicateInTrash(url);

            return ImportResult.DuplicateExistingItem(url);
        }

        // 5b. Duplicate 检查 — import_tasks
        var existingTask = await _taskRepo.GetByNormalizedUrlAsync(normalizedUrl);
        if (existingTask != null)
        {
            // completed → 检查关联 item 是否存在
            if (existingTask.Status == Models.Enums.TaskStatus.completed)
            {
                if (existingTask.ItemId.HasValue)
                {
                    var taskItem = await _itemRepo.GetByIdAsync(existingTask.ItemId.Value);
                    if (taskItem != null)
                    {
                        // item 存在但可能在回收站
                        if (taskItem.DeletedAt != null)
                            return ImportResult.DuplicateInTrash(url);
                        return ImportResult.DuplicateExistingItem(url, taskItem.Id);
                    }
                    // item 不存在但 task completed → orphan task，允许重新导入
                }
                else
                {
                    // completed 但无 item_id → orphan task，允许重新导入
                }
            }

            // pending 且无 error_message → 真实任务进行中
            if (existingTask.Status == Models.Enums.TaskStatus.pending && string.IsNullOrEmpty(existingTask.ErrorMessage))
                return ImportResult.DuplicateImportTask(url);

            // 其它状态（failed, 有 error_message 的 pending）→ 允许重新导入
        }

        // 6. 创建 import_task
        var task = new ImportTask
        {
            Id = Guid.NewGuid(),
            OriginalUrl = url,
            NormalizedUrl = normalizedUrl,
            Platform = platform.Value,
            Status = Models.Enums.TaskStatus.pending,
            CreatedAt = DateTimeOffset.UtcNow
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

        // 9. Success: 写入 item
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
