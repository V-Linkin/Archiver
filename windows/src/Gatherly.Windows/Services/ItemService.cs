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
    /// 更新备注
    /// 对齐 macOS ItemService.updateRemark() 语义
    /// </summary>
    public async Task<Item> UpdateRemarkAsync(Item item, string? remark)
    {
        var fresh = await _itemRepo.GetByIdAsync(item.Id)
            ?? throw new InvalidOperationException($"Item not found: {item.Id}");

        fresh.Remark = string.IsNullOrWhiteSpace(remark) ? null : remark;
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        return fresh;
    }

    /// <summary>
    /// 将内容移入回收站
    /// 对齐 macOS ItemService.trashItem() 语义
    /// </summary>
    public async Task TrashItemAsync(Item item, IReadOnlyList<string>? mediaPaths = null)
    {
        var fresh = await _itemRepo.GetByIdAsync(item.Id)
            ?? throw new InvalidOperationException($"Item not found: {item.Id}");

        var now = DateTimeOffset.UtcNow;

        fresh.DeletedAt = now;
        fresh.ContentStatus = ContentStatus.trashed;
        await _itemRepo.UpdateAsync(fresh);

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

    /// <summary>
    /// 从回收站恢复内容
    /// 对齐 macOS TrashView.restoreItem() 语义
    /// </summary>
    public async Task RestoreItemAsync(Item item)
    {
        var record = await _trashRepo.GetByItemIdAsync(item.Id)
            ?? throw new InvalidOperationException($"TrashRecord not found for item: {item.Id}");

        var fresh = await _itemRepo.GetByIdAsync(item.Id)
            ?? throw new InvalidOperationException($"Item not found: {item.Id}");

        fresh.DeletedAt = null;
        fresh.ContentStatus = ContentStatus.normal;
        fresh.ArchiveStatus = record.OriginalArchiveStatus;
        fresh.FolderId = record.OriginalFolderId;
        await _itemRepo.UpdateAsync(fresh);

        await _trashRepo.DeleteByItemIdAsync(item.Id);
    }

    /// <summary>
    /// 永久删除 item
    /// 依赖外键 cascade 删除 media_assets / trash_records
    /// 本轮不删除真实媒体文件（macOS 有文件系统删除，Windows 暂未实现）
    /// </summary>
    public async Task PermanentlyDeleteItemAsync(Item item)
    {
        var record = await _trashRepo.GetByItemIdAsync(item.Id);
        if (record != null)
        {
            await _trashRepo.DeleteByItemIdAsync(item.Id);
        }

        await _itemRepo.DeleteAsync(item.Id);
    }
}
