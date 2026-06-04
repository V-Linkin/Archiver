import Foundation

/// 文件夹业务操作服务
final class FolderService: @unchecked Sendable {
    private let folderRepo: FolderRepository
    private let itemRepo: ItemRepository

    init(
        folderRepo: FolderRepository = FolderRepository(),
        itemRepo: ItemRepository = ItemRepository()
    ) {
        self.folderRepo = folderRepo
        self.itemRepo = itemRepo
    }

    /// 创建文件夹
    func createFolder(
        name: String,
        platform: Platform,
        parentID: UUID? = nil,
        customPlatformID: UUID? = nil
    ) throws -> Folder {
        let folder = Folder(
            name: name,
            parentID: parentID,
            platform: platform,
            customPlatformID: customPlatformID
        )
        try folderRepo.insert(folder)
        return folder
    }
    
    /// 重命名文件夹
    func renameFolder(_ folder: Folder, newName: String) throws {
        var updated = folder
        updated.name = newName
        try folderRepo.update(updated)
    }
    
    /// 删除文件夹：先将 folder 内所有 items 的 folderID 清空，再删除文件夹本身
    func deleteFolder(id: UUID) throws {
        let itemsInFolder = (try? itemRepo.fetchAll(folderID: id)) ?? []
        for var item in itemsInFolder {
            item.folderID = nil
            try? itemRepo.update(item)
        }
        try folderRepo.delete(id: id)
    }
}
