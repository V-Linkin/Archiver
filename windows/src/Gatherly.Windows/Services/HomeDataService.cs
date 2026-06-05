using Gatherly.Windows.Database;
using Gatherly.Windows.Models;

namespace Gatherly.Windows.Services;

/// <summary>
/// 首页数据服务 — 只负责读取最近内容
/// </summary>
public class HomeDataService
{
    private readonly ItemRepository _itemRepo;

    public HomeDataService(ItemRepository itemRepo)
    {
        _itemRepo = itemRepo;
    }

    /// <summary>
    /// 获取最近导入的内容，按 importDate 倒序
    /// </summary>
    public async Task<List<Item>> GetRecentItemsAsync(int limit = 20)
    {
        return await _itemRepo.GetRecentAsync(limit);
    }
}
