using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services;

/// <summary>
/// 首页数据服务 — 读取最近内容及首图路径
/// </summary>
public class HomeDataService
{
    private readonly ItemRepository _itemRepo;
    private readonly MediaRepository _mediaRepo;

    public HomeDataService(ItemRepository itemRepo, MediaRepository mediaRepo)
    {
        _itemRepo = itemRepo;
        _mediaRepo = mediaRepo;
    }

    /// <summary>
    /// 获取最近导入的内容，按 importDate 倒序
    /// </summary>
    public async Task<List<Item>> GetRecentItemsAsync(int limit = 20)
    {
        return await _itemRepo.GetRecentAsync(limit);
    }

    /// <summary>
    /// 获取指定 item 的首张图片本地完整路径，无图片返回 null
    /// </summary>
    public async Task<string?> GetFirstImagePathAsync(Guid itemId)
    {
        var assets = await _mediaRepo.GetByItemIdAsync(itemId);
        var first = assets.FirstOrDefault(a =>
            a.Type == MediaType.cover || a.Type == MediaType.image);
        if (first?.LocalPath == null) return null;
        return MediaPathHelper.ResolveFullPath(first.LocalPath);
    }

    /// <summary>
    /// 批量获取多个 item 的首图路径
    /// </summary>
    public async Task<Dictionary<Guid, string>> GetFirstImagePathsAsync(IEnumerable<Guid> itemIds)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var id in itemIds)
        {
            var path = await GetFirstImagePathAsync(id);
            if (path != null)
                result[id] = path;
        }
        return result;
    }
}
