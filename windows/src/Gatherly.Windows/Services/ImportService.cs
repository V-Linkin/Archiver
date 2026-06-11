using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services.Import;
using Gatherly.Windows.Services.Parsers;
using Gatherly.Windows.Services.Url;

namespace Gatherly.Windows.Services;

/// <summary>
/// 导入服务 — Phase 7C: 完整导入管道
/// URL 提取 → 平台识别 → 去重检查 → 创建任务 → Router → Parser
/// </summary>
public class ImportService
{
    private readonly ItemRepository _itemRepo;
    private readonly ImportTaskRepository _taskRepo;
    private readonly PlatformRouter _router;

    public ImportService(ItemRepository itemRepo, ImportTaskRepository taskRepo)
    {
        _itemRepo = itemRepo;
        _taskRepo = taskRepo;
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

        // 5. Duplicate 检查
        var existingItem = await _itemRepo.GetByNormalizedUrlAsync(normalizedUrl);
        if (existingItem != null)
            return ImportResult.Duplicate(url, "该链接已存在于归档库中。");

        var existingTask = await _taskRepo.GetByNormalizedUrlAsync(normalizedUrl);
        if (existingTask != null && existingTask.Status != Models.Enums.TaskStatus.failed)
            return ImportResult.Duplicate(url, "该链接已有导入任务。");

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

        // 8. 更新任务状态并返回结果
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

        // 9. 成功（后续 Phase 7D 实现真实写入）
        await _taskRepo.UpdateStatusAsync(task.Id, Models.Enums.TaskStatus.completed);
        return ImportResult.TaskCreated(task.Id, url, platform.Value, contentId);
    }
}
