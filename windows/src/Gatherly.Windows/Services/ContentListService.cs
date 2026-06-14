using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services;

/// <summary>
/// 内容列表数据服务 — 平台/文件夹/自定义平台/未分类的 items 和 folders 读取
/// </summary>
public class ContentListService
{
    private readonly ItemRepository _itemRepo;
    private readonly FolderRepository _folderRepo;

    public ContentListService(ItemRepository itemRepo, FolderRepository folderRepo)
    {
        _itemRepo = itemRepo;
        _folderRepo = folderRepo;
    }

    /// <summary>
    /// 获取指定平台的 items
    /// </summary>
    public async Task<List<Item>> GetPlatformItemsAsync(Platform platform, int limit = 100)
    {
        return await _itemRepo.GetByPlatformAsync(platform, limit);
    }

    /// <summary>
    /// 获取指定平台的顶层文件夹
    /// </summary>
    public async Task<List<Folder>> GetPlatformFoldersAsync(Platform platform)
    {
        return await _folderRepo.GetByPlatformAsync(platform);
    }

    /// <summary>
    /// 获取指定文件夹的 items
    /// </summary>
    public async Task<List<Item>> GetFolderItemsAsync(Guid folderId, int limit = 100)
    {
        return await _itemRepo.GetByFolderIdAsync(folderId, limit);
    }

    /// <summary>
    /// 获取指定文件夹的子文件夹
    /// </summary>
    public async Task<List<Folder>> GetChildFoldersAsync(Guid folderId)
    {
        return await _folderRepo.GetByParentIdAsync(folderId);
    }

    /// <summary>
    /// 获取合并平台的 items（标准 platform + custom_platforms，去重）
    /// </summary>
    public async Task<List<Item>> GetMergedPlatformItemsAsync(Platform platform, List<Guid> customPlatformIds, int limit = 100)
    {
        return await _itemRepo.GetByPlatformWithCustomAsync(platform, customPlatformIds, limit);
    }

    /// <summary>
    /// 获取合并平台的顶层文件夹（标准 platform + custom_platforms）
    /// </summary>
    public async Task<List<Folder>> GetMergedPlatformFoldersAsync(Platform platform, List<Guid> customPlatformIds)
    {
        var standardFolders = await _folderRepo.GetByPlatformAsync(platform);
        var customFolders = new List<Folder>();
        foreach (var cpId in customPlatformIds)
        {
            customFolders.AddRange(await _folderRepo.GetByCustomPlatformIdAsync(cpId));
        }
        return standardFolders.Concat(customFolders).ToList();
    }

    /// <summary>
    /// 获取指定自定义平台的 items
    /// </summary>
    public async Task<List<Item>> GetCustomPlatformItemsAsync(Guid customPlatformId)
    {
        return await _itemRepo.GetByCustomPlatformIdAsync(customPlatformId);
    }

    /// <summary>
    /// 获取指定自定义平台的顶层文件夹
    /// </summary>
    public async Task<List<Folder>> GetCustomPlatformFoldersAsync(Guid customPlatformId)
    {
        return await _folderRepo.GetByCustomPlatformIdAsync(customPlatformId);
    }

    /// <summary>
    /// 获取未分类 items（platform=custom 且 custom_platform_id 为 null）
    /// </summary>
    public async Task<List<Item>> GetUncategorizedItemsAsync()
    {
        return await _itemRepo.GetUncategorizedItemsAsync();
    }

    /// <summary>
    /// 获取未分类文件夹
    /// </summary>
    public async Task<List<Folder>> GetUncategorizedFoldersAsync()
    {
        return await _folderRepo.GetUncategorizedFoldersAsync();
    }
}
