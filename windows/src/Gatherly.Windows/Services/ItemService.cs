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

    private readonly FolderRepository _folderRepo;

    public ItemService(ItemRepository itemRepo, TrashRepository trashRepo, FolderRepository folderRepo)
    {
        _itemRepo = itemRepo;
        _trashRepo = trashRepo;
        _folderRepo = folderRepo;
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

        // Check if folder exists before referencing it (avoid FK violation)
        Guid? validFolderId = null;
        if (fresh.FolderId != null)
        {
            var folderExists = await _folderRepo.ExistsAsync(fresh.FolderId.Value);
            if (folderExists)
                validFolderId = fresh.FolderId;
        }

        // Get raw item_id from database to match FK case exactly
        var rawItemId = await _itemRepo.GetRawIdAsync(fresh.Id);

        var record = new TrashRecord
        {
            Id = Guid.NewGuid(),
            ItemId = fresh.Id,
            RawItemId = rawItemId,
            DeletedAt = now,
            AutoDeleteAt = now.AddDays(30),
            OriginalFolderId = validFolderId,
            OriginalArchiveStatus = fresh.ArchiveStatus,
            MediaPaths = mediaPaths?.ToList() ?? new List<string>()
        };

        await _trashRepo.InsertAsync(record);
    }

    /// <summary>
    /// 从回收站恢复内容
    /// 对齐 macOS TrashView.restoreItem() 语义
    /// 兼容历史脏数据：无 trash_record 时仍可恢复
    /// </summary>
    public async Task RestoreItemAsync(Item item)
    {
        var fresh = await _itemRepo.GetByIdAsync(item.Id)
            ?? throw new InvalidOperationException($"Item not found: {item.Id}");

        var record = await _trashRepo.GetByItemIdAsync(item.Id);

        fresh.DeletedAt = null;
        fresh.ContentStatus = ContentStatus.normal;

        // 如果有 trash_record，恢复原始状态
        if (record != null)
        {
            fresh.ArchiveStatus = record.OriginalArchiveStatus;
            fresh.FolderId = record.OriginalFolderId;
            await _trashRepo.DeleteByItemIdAsync(item.Id);
        }

        await _itemRepo.UpdateAsync(fresh);
    }

    /// <summary>
    /// 永久删除 item
    /// 依赖外键 cascade 删除 media_assets / trash_records
    /// 本轮不删除真实媒体文件（macOS 有文件系统删除，Windows 暂未实现）
    /// </summary>
    public async Task PermanentlyDeleteItemAsync(Item item)
    {
        // Delete trash_record if exists (may not exist for old orphaned data)
        await _trashRepo.DeleteByItemIdAsync(item.Id);
        await _itemRepo.DeleteAsync(item.Id);
    }
}
