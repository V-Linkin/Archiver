using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.Services;

/// <summary>
/// Item 业务服务 — 封装 Item 的写入操作
/// </summary>
public class ItemService
{
    private readonly ItemRepository _itemRepo;
    private readonly TrashRepository _trashRepo;
    private readonly MediaRepository _mediaRepo;
    private readonly FolderRepository _folderRepo;
    private readonly SqliteConnection _connection;

    public ItemService(ItemRepository itemRepo, TrashRepository trashRepo,
        FolderRepository folderRepo, MediaRepository mediaRepo, SqliteConnection connection)
    {
        _itemRepo = itemRepo;
        _trashRepo = trashRepo;
        _folderRepo = folderRepo;
        _mediaRepo = mediaRepo;
        _connection = connection;
    }

    /// <summary>
    /// 更新备注
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
    /// </summary>
    public async Task TrashItemAsync(Item item, IReadOnlyList<string>? mediaPaths = null)
    {
        var fresh = await _itemRepo.GetByIdAsync(item.Id)
            ?? throw new InvalidOperationException($"Item not found: {item.Id}");

        var now = DateTimeOffset.UtcNow;

        fresh.DeletedAt = now;
        fresh.ContentStatus = ContentStatus.trashed;
        await _itemRepo.UpdateAsync(fresh);

        Guid? validFolderId = null;
        if (fresh.FolderId != null)
        {
            var folderExists = await _folderRepo.ExistsAsync(fresh.FolderId.Value);
            if (folderExists)
                validFolderId = fresh.FolderId;
        }

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
    /// </summary>
    public async Task RestoreItemAsync(Item item)
    {
        var fresh = await _itemRepo.GetByIdAsync(item.Id)
            ?? throw new InvalidOperationException($"Item not found: {item.Id}");

        // 恢复冲突检查：如果归档库中已存在相同 URL 的活跃 item，阻止恢复
        var existingActive = await _itemRepo.GetByNormalizedUrlAsync(fresh.NormalizedUrl);
        if (existingActive != null && existingActive.Id != fresh.Id)
        {
            throw new InvalidOperationException("归档库中已存在相同内容，请先删除现有内容后再恢复。");
        }

        var record = await _trashRepo.GetByItemIdAsync(item.Id);

        fresh.DeletedAt = null;
        fresh.ContentStatus = ContentStatus.normal;

        if (record != null)
        {
            fresh.ArchiveStatus = record.OriginalArchiveStatus;
            fresh.FolderId = record.OriginalFolderId;
            await _trashRepo.DeleteByItemIdAsync(item.Id);
        }

        await _itemRepo.UpdateAsync(fresh);
    }

    /// <summary>
    /// 永久删除 item — 按正确顺序清理所有引用
    /// </summary>
    public async Task PermanentlyDeleteItemAsync(Item item)
    {
        var itemId = item.Id;

        // 1. 删除 trash_record（如果有）
        await _trashRepo.DeleteByItemIdAsync(itemId);

        // 2. 删除 media_assets（手动删除，不依赖 cascade）
        await _mediaRepo.DeleteByItemIdAsync(itemId);

        // 3. 清理 import_tasks.item_id 引用（保留任务历史，置 NULL）
        await ClearImportTaskItemRefAsync(itemId);

        // 4. 删除 items_fts 记录
        await DeleteFtsByRowIdAsync(itemId);

        // 5. 最后删除 items
        await _itemRepo.DeleteAsync(itemId);
    }

    /// <summary>
    /// 清理 import_tasks 中指向该 item 的 item_id（置 NULL，保留任务历史）
    /// </summary>
    private async Task ClearImportTaskItemRefAsync(Guid itemId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE import_tasks SET item_id = NULL WHERE item_id COLLATE NOCASE = $id";
        cmd.Parameters.AddWithValue("$id", itemId.ToString("D"));
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 删除 items_fts 中对应的记录
    /// </summary>
    private async Task DeleteFtsByRowIdAsync(Guid itemId)
    {
        // 找到该 item 的 rowid
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT rowid FROM items WHERE id COLLATE NOCASE = $id";
        cmd.Parameters.AddWithValue("$id", itemId.ToString("D"));
        var rowid = await cmd.ExecuteScalarAsync();
        if (rowid != null)
        {
            using var ftsCmd = _connection.CreateCommand();
            ftsCmd.CommandText = "DELETE FROM items_fts WHERE rowid = $rowid";
            ftsCmd.Parameters.AddWithValue("$rowid", rowid);
            await ftsCmd.ExecuteNonQueryAsync();
        }
    }
}
