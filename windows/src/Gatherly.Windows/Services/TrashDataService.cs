using Gatherly.Windows.Database;
using Gatherly.Windows.Models;

namespace Gatherly.Windows.Services;

/// <summary>
/// 回收站数据服务 — 只负责读取已删除内容和回收站记录
/// </summary>
public class TrashDataService
{
    private readonly ItemRepository _itemRepo;
    private readonly TrashRepository _trashRepo;

    public TrashDataService(ItemRepository itemRepo, TrashRepository trashRepo)
    {
        _itemRepo = itemRepo;
        _trashRepo = trashRepo;
    }

    /// <summary>
    /// 获取回收站中的所有 items（已删除）
    /// </summary>
    public async Task<List<Item>> GetTrashedItemsAsync()
    {
        return await _itemRepo.GetTrashedAsync();
    }

    /// <summary>
    /// 获取指定 item 的回收站记录
    /// </summary>
    public async Task<TrashRecord?> GetTrashRecordAsync(Guid itemId)
    {
        return await _trashRepo.GetByItemIdAsync(itemId);
    }
}
