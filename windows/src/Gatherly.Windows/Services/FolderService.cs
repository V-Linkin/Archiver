using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services;

/// <summary>
/// 文件夹业务服务 — 对齐 macOS FolderService
/// </summary>
public class FolderService
{
    private readonly FolderRepository _folderRepo;
    private readonly ItemRepository _itemRepo;

    public FolderService(FolderRepository folderRepo, ItemRepository itemRepo)
    {
        _folderRepo = folderRepo;
        _itemRepo = itemRepo;
    }

    public async Task<Folder> CreateFolderAsync(string name, Platform platform,
        Guid? parentPlatformId = null, Guid? customPlatformId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("文件夹名称不能为空");

        return await _folderRepo.CreateAsync(name.Trim(), platform, parentPlatformId, customPlatformId);
    }

    public async Task RenameFolderAsync(Guid folderId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("文件夹名称不能为空");

        var existing = await _folderRepo.GetByIdAsync(folderId)
            ?? throw new InvalidOperationException("文件夹不存在");

        await _folderRepo.UpdateNameAsync(folderId, newName.Trim());
    }

    public async Task DeleteFolderAsync(Guid folderId)
    {
        var existing = await _folderRepo.GetByIdAsync(folderId)
            ?? throw new InvalidOperationException("文件夹不存在");

        // 对齐 macOS：清空文件夹内所有 items 的 folderID，但不删除 items
        var itemsInFolder = await _itemRepo.GetByFolderIdAsync(folderId);
        foreach (var item in itemsInFolder)
        {
            item.FolderId = null;
            item.ModifyDate = DateTimeOffset.UtcNow;
            await _itemRepo.UpdateAsync(item);
        }

        await _folderRepo.DeleteAsync(folderId);
    }

    public async Task<int> CountItemsAsync(Guid folderId)
    {
        return await _folderRepo.CountItemsAsync(folderId);
    }
}
