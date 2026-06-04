import Foundation

/// 内容服务 — 封装 Item 的业务操作，减少 View 直接调用 Repository
final class ItemService: @unchecked Sendable {
    private let itemRepo: ItemRepository
    private let trashRepo: TrashRepository
    private let folderRepo: FolderRepository

    init(
        itemRepo: ItemRepository = ItemRepository(),
        trashRepo: TrashRepository = TrashRepository(),
        folderRepo: FolderRepository = FolderRepository()
    ) {
        self.itemRepo = itemRepo
        self.trashRepo = trashRepo
        self.folderRepo = folderRepo
    }

    /// 更新备注
    func updateRemark(_ item: Item, remark: String?) throws -> Item {
        guard var updated = try itemRepo.find(id: item.id) else {
            throw NSError(domain: "ItemService", code: 1, userInfo: [NSLocalizedDescriptionKey: "Item not found"])
        }
        updated.remark = remark
        updated.modifyDate = Date()
        try itemRepo.update(updated)
        return updated
    }
    
    /// 移动到文件夹
    /// 如果目标文件夹属于自定义平台，同步更新 customPlatformID 和 platform
    func moveToFolder(itemID: UUID, folderID: UUID) throws -> Item {
        guard var item = try itemRepo.find(id: itemID) else {
            throw NSError(domain: "ItemService", code: 1, userInfo: [NSLocalizedDescriptionKey: "Item not found"])
        }
        item.folderID = folderID
        // 如果目标文件夹属于自定义平台，同步更新 item 的平台归属
        if let folder = try? folderRepo.find(id: folderID),
           let cpID = folder.customPlatformID {
            item.customPlatformID = cpID
            item.platform = .custom
        }
        try itemRepo.update(item)
        return item
    }
    
    /// 移动到自定义平台
    /// 设置 customPlatformID、platform = .custom，清空 folderID
    func moveToCustomPlatform(itemID: UUID, customPlatformID: UUID) throws -> Item {
        guard var item = try itemRepo.find(id: itemID) else {
            throw NSError(domain: "ItemService", code: 1, userInfo: [NSLocalizedDescriptionKey: "Item not found"])
        }
        item.customPlatformID = customPlatformID
        item.platform = .custom
        item.folderID = nil
        try itemRepo.update(item)
        return item
    }
    
    /// 将内容移入回收站
    /// 设置 deletedAt、contentStatus = .trashed，创建 TrashRecord
    /// 保留原 folderID 和 archiveStatus
    /// mediaPaths: 关联的媒体文件本地路径，用于恢复时重建文件映射
    func trashItem(_ item: Item, mediaPaths: [String] = []) throws {
        var updated = item
        updated.deletedAt = Date()
        updated.contentStatus = .trashed
        try itemRepo.update(updated)

        let record = TrashRecord(
            itemID: item.id,
            originalFolderID: item.folderID,
            originalArchiveStatus: item.archiveStatus,
            mediaPaths: mediaPaths
        )
        try trashRepo.insert(record)
    }
}
