using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services;

/// <summary>
/// Item 业务服务 — 封装 Item 的写入操作
/// </summary>
public class ItemService
{
    private readonly ItemRepository _itemRepo;
    private readonly TrashRepository _trashRepo;

    public ItemService(ItemRepository itemRepo, TrashRepository trashRepo)
    {
        _itemRepo = itemRepo;
        _trashRepo = trashRepo;
    }

    /// <summary>
    /// 将内容移入回收站
    /// 对齐 macOS ItemService.trashItem() 语义
    /// </summary>
    public async Task TrashItemAsync(Item item, IReadOnlyList<string>? mediaPaths = null)
    {
        // 从数据库获取最新版本
        var fresh = await _itemRepo.GetByIdAsync(item.Id)
            ?? throw new InvalidOperationException($"Item not found: {item.Id}");

        var now = DateTimeOffset.UtcNow;

        // 更新 item 状态
        fresh.DeletedAt = now;
        fresh.ContentStatus = ContentStatus.trashed;
        await _itemRepo.UpdateAsync(fresh);

        // 创建回收站记录
        var record = new TrashRecord
        {
            Id = Guid.NewGuid(),
            ItemId = fresh.Id,
            DeletedAt = now,
            AutoDeleteAt = now.AddDays(30),
            OriginalFolderId = fresh.FolderId,
            OriginalArchiveStatus = fresh.ArchiveStatus,
            MediaPaths = mediaPaths?.ToList() ?? new List<string>()
        };
        await _trashRepo.InsertAsync(record);
    }
}
