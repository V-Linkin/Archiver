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

    /// 将内容移入回收站
    /// 设置 deletedAt、contentStatus = .trashed，创建 TrashRecord
    /// 保留原 folderID 和 archiveStatus
    func trashItem(_ item: Item) throws {
        var updated = item
        updated.deletedAt = Date()
        updated.contentStatus = .trashed
        try itemRepo.update(updated)

        let record = TrashRecord(
            itemID: item.id,
            originalFolderID: item.folderID,
            originalArchiveStatus: item.archiveStatus
        )
        try trashRepo.insert(record)
    }
}
