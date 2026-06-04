import Foundation

/// 内容服务 — 封装 Item 的业务操作，减少 View 直接调用 Repository
final class ItemService: @unchecked Sendable {
    private let itemRepo: ItemRepository
    private let trashRepo: TrashRepository

    init(
        itemRepo: ItemRepository = ItemRepository(),
        trashRepo: TrashRepository = TrashRepository()
    ) {
        self.itemRepo = itemRepo
        self.trashRepo = trashRepo
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
