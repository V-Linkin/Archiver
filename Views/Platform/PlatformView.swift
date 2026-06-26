import SwiftUI

struct PlatformView: View {
    let platform: Platform
    @Binding var selectedNav: NavigationTarget?
    @Binding var previousNav: NavigationTarget?
    var openDetail: (UUID) -> Void
    @Environment(AppState.self) private var appState
    private let itemService = ItemService()
    @State private var viewMode: ViewMode = .grid
    @State private var sortNewestFirst = true
    @State private var showNewFolderSheet = false
    @State private var moveTargetItemID: UUID?
    @State private var showMoveOverlay = false
    @State private var showMoveToPlatform = false
    @State private var isMultiSelectMode = false
    @State private var selectedItemIDs: Set<UUID> = []
    @State private var showBatchDeleteConfirm = false
    @State private var pageJumpText: String = ""

    // 滚动控制
    @State private var scrollResetToken = UUID()
    @State private var pendingAnchorID: UUID? = nil

    private var stableID: UUID {
        let s = "system:\(platform.rawValue)"
        var h: UInt64 = 14695981039346656037
        for b in s.utf8 {
            h ^= UInt64(b)
            h = h &* 1099511628211
        }
        let hex = String(format: "%016llx", h)
        let p1 = hex.prefix(8)
        let p2 = hex.dropFirst(8).prefix(4)
        let p3 = hex.dropFirst(12).prefix(3)
        let p4 = hex.dropFirst(15).prefix(3)
        let p5 = hex.dropFirst(4).prefix(12)
        return UUID(uuidString: "\(p1)-\(p2)-4\(p3)-8\(p4)-\(p5)")!
    }

    private var listState: PlatformListState {
        get { appState.platformListStates[stableID] ?? PlatformListState() }
        set { appState.platformListStates[stableID] = newValue }
    }

    enum ViewMode: String, CaseIterable {
        case grid = "网格"
        case list = "列表"
        var icon: String {
            switch self {
            case .grid: return "square.grid.2x2"
            case .list: return "list.bullet"
            }
        }
    }

    var body: some View {
        VStack(spacing: 0) {
            paginationBar
            if !listState.folders.isEmpty { folderSection }
            if listState.items.isEmpty && !listState.isLoadingPage && listState.hasLoadedOnce { emptyState }
            else if viewMode == .grid { mediaGridView }
            else { textListView }
        }
        .navigationTitle(platform.displayName)
        .onChange(of: viewMode) { _, newValue in
            UserDefaults.standard.set(newValue.rawValue, forKey: "viewMode_\(platform.rawValue)")
        }
        .toolbar {
            ToolbarItem(placement: .primaryAction) {
                if isMultiSelectMode {
                    HStack(spacing: 8) {
                        Text("已选 \(selectedItemIDs.count) 项")
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                        Button("移动") { showMoveToPlatform = true }
                            .disabled(selectedItemIDs.isEmpty)
                        Button("删除", role: .destructive) { showBatchDeleteConfirm = true }
                            .disabled(selectedItemIDs.isEmpty)
                        Button("取消") { isMultiSelectMode = false; selectedItemIDs.removeAll() }
                    }
                } else {
                    HStack(spacing: 8) {
                        Button(action: { appState.newItemPlatform = platform; appState.newItemCustomPlatformID = nil; appState.showNewItem = true }) {
                            Image(systemName: "plus.circle")
                        }
                        Button(action: { showNewFolderSheet = true }) {
                            Image(systemName: "folder.badge.plus")
                        }
                        Button(action: { sortNewestFirst.toggle(); changePage(1) }) {
                            Image(systemName: sortNewestFirst ? "arrow.down" : "arrow.up")
                        }
                        .help(sortNewestFirst ? "最新优先" : "最早优先")
                        Picker("视图", selection: $viewMode) {
                            ForEach(ViewMode.allCases, id: \.self) { mode in
                                Image(systemName: mode.icon).tag(mode)
                            }
                        }
                        .pickerStyle(.segmented)
                        .frame(width: 80)
                    }
                }
            }
        }
        .sheet(isPresented: $showNewFolderSheet) {
            NewFolderSheet(platform: platform, isPresented: $showNewFolderSheet) { loadPage(listState.currentPage) }
        }
        .overlay {
            if showMoveOverlay, let itemID = moveTargetItemID {
                Color.black.opacity(0.3).ignoresSafeArea()
                    .onTapGesture { showMoveOverlay = false }
                MoveToFolderOverlay(itemID: itemID, isPresented: $showMoveOverlay)
            }
        }
        .sheet(isPresented: $showMoveToPlatform) {
            if isMultiSelectMode {
                MoveToPlatformSheet(itemID: nil, itemIDs: Array(selectedItemIDs), isPresented: $showMoveToPlatform) {
                    isMultiSelectMode = false
                    selectedItemIDs.removeAll()
                    loadPage(listState.currentPage)
                }
            } else if let itemID = moveTargetItemID {
                MoveToPlatformSheet(itemID: itemID, isPresented: $showMoveToPlatform) { loadPage(listState.currentPage) }
            }
        }
        .onAppear {
            if let saved = UserDefaults.standard.string(forKey: "viewMode_\(platform.rawValue)"),
               let mode = ViewMode(rawValue: saved) {
                viewMode = mode
            }
            if !listState.hasLoadedOnce || listState.items.isEmpty {
                restoreBrowseState()
            }
        }
        .onDisappear {
            if !listState.items.isEmpty {
                saveBrowseState()
            }
        }
        .onChange(of: appState.deletionEventCounter) { _, _ in
            reloadAfterDeletion()
        }
        .alert("确认删除", isPresented: $showBatchDeleteConfirm) {
            Button("取消", role: .cancel) {}
            Button("删除", role: .destructive) { batchDeleteItems() }
        } message: {
            Text("确定要删除选中的 \(selectedItemIDs.count) 条内容吗？删除后可在回收站恢复。")
        }
    }

    // MARK: - 分页控制栏

    private var paginationBar: some View {
        HStack(spacing: 10) {
            Text("共 \(listState.totalCount) 条")
                .font(.caption)
                .foregroundStyle(.secondary)
                .fixedSize()

            Spacer()

            Button { changePage(1) } label: {
                Image(systemName: "backward.end.fill")
            }
            .disabled(listState.currentPage <= 1 || listState.isLoadingPage)
            .help("首页")

            Button { changePage(listState.currentPage - 1) } label: {
                Image(systemName: "chevron.left")
            }
            .disabled(listState.currentPage <= 1 || listState.isLoadingPage)
            .help("上一页")

            Text("第 \(listState.currentPage) / \(listState.totalPages) 页")
                .font(.caption)
                .monospacedDigit()
                .fixedSize()

            Button { changePage(listState.currentPage + 1) } label: {
                Image(systemName: "chevron.right")
            }
            .disabled(listState.currentPage >= listState.totalPages || listState.isLoadingPage)
            .help("下一页")

            Button { changePage(listState.totalPages) } label: {
                Image(systemName: "forward.end.fill")
            }
            .disabled(listState.currentPage >= listState.totalPages || listState.isLoadingPage)
            .help("末页")

            if listState.totalPages > 1 {
                HStack(spacing: 4) {
                    TextField("跳转", text: $pageJumpText)
                        .textFieldStyle(.roundedBorder)
                        .frame(width: 40)
                        .font(.caption)
                        .monospacedDigit()
                        .onSubmit { jumpToInputPage() }
                        .onChange(of: pageJumpText) { _, newValue in
                            pageJumpText = newValue.filter { $0.isNumber }
                        }
                    Text("/ \(listState.totalPages)")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .monospacedDigit()
                        .fixedSize()
                }
            }
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 6)
        .background(.bar)
    }

    // MARK: - 内容视图

    private var folderSection: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 10) {
                ForEach(listState.folders) { folder in
                    Button {
                        previousNav = .platform(platform)
                        selectedNav = .folder(folder.id)
                    } label: {
                        HStack(spacing: 6) {
                            Image(systemName: "folder.fill").foregroundStyle(.blue)
                            Text(folder.name).font(.subheadline)
                        }
                        .padding(.horizontal, 12)
                        .padding(.vertical, 6)
                        .background(.background)
                        .clipShape(Capsule())
                        .overlay(Capsule().stroke(.quaternary))
                    }
                    .buttonStyle(.plain)
                }
            }
            .padding(.horizontal, 16)
            .padding(.vertical, 8)
        }
    }

    private var mediaGridView: some View {
        ScrollViewReader { proxy in
            ScrollView {
                LazyVGrid(columns: [GridItem(.adaptive(minimum: 180, maximum: 220), spacing: 16)], spacing: 16) {
                    ForEach(listState.items) { item in
                        Button {
                            if isMultiSelectMode {
                                if selectedItemIDs.contains(item.id) {
                                    selectedItemIDs.remove(item.id)
                                    if selectedItemIDs.isEmpty { isMultiSelectMode = false }
                                } else {
                                    selectedItemIDs.insert(item.id)
                                }
                            } else {
                                saveBrowseState(anchorItemID: item.id)
                                previousNav = .platform(platform)
                                if NavDebounce.shared.canNavigate() { openDetail(item.id) }
                            }
                        } label: {
                            ZStack(alignment: .topTrailing) {
                                ItemCardView(item: item)
                                if isMultiSelectMode {
                                    Color.black.opacity(selectedItemIDs.contains(item.id) ? 0.15 : 0)
                                        .clipShape(RoundedRectangle(cornerRadius: 10))
                                    Image(systemName: selectedItemIDs.contains(item.id) ? "checkmark.circle.fill" : "circle")
                                        .font(.system(size: 28))
                                        .foregroundStyle(selectedItemIDs.contains(item.id) ? .blue : .white)
                                        .padding(8)
                                }
                            }
                            .id(item.id)
                        }
                        .buttonStyle(.plain)
                        .contextMenu { itemContextMenu(item) }
                    }
                }
                .padding(16)
            }
            .id(scrollResetToken)
            .onAppear { restoreAnchorIfNeeded(proxy: proxy) }
        }
    }

    private var textListView: some View {
        ScrollViewReader { proxy in
            List(listState.items) { item in
                Button {
                    if isMultiSelectMode {
                        if selectedItemIDs.contains(item.id) {
                            selectedItemIDs.remove(item.id)
                            if selectedItemIDs.isEmpty { isMultiSelectMode = false }
                        } else {
                            selectedItemIDs.insert(item.id)
                        }
                    } else {
                        saveBrowseState(anchorItemID: item.id)
                        previousNav = .platform(platform)
                        if NavDebounce.shared.canNavigate() { openDetail(item.id) }
                    }
                } label: {
                    HStack {
                        if isMultiSelectMode {
                            Image(systemName: selectedItemIDs.contains(item.id) ? "checkmark.circle.fill" : "circle")
                                .font(.title2)
                                .foregroundStyle(selectedItemIDs.contains(item.id) ? .blue : .secondary)
                                .frame(width: 28)
                        }
                        ItemListRow(item: item)
                    }
                }
                .buttonStyle(.plain)
                .contextMenu { itemContextMenu(item) }
                .id(item.id)
            }
            .listStyle(.plain)
            .id(scrollResetToken)
            .onAppear { restoreAnchorIfNeeded(proxy: proxy) }
        }
    }

    private var emptyState: some View {
        VStack(spacing: 16) {
            Image(systemName: platform.iconName)
                .font(.system(size: 48))
                .foregroundStyle(.tertiary)
            Text("\(platform.displayName)暂无内容")
                .font(.title3)
                .foregroundStyle(.secondary)
            Text("回到首页粘贴一条链接导入")
                .font(.subheadline)
                .foregroundStyle(.tertiary)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }

    // MARK: - 上下文菜单

    @ViewBuilder
    private func itemContextMenu(_ item: Item) -> some View {
        Button {
            isMultiSelectMode = true
            selectedItemIDs.insert(item.id)
        } label: {
            Label("多选", systemImage: "checkmark.circle")
        }
        Button {
            moveTargetItemID = item.id
            showMoveOverlay = true
        } label: {
            Label(listState.folders.isEmpty ? "暂无文件夹" : "移动到文件夹", systemImage: "folder")
        }
        .disabled(listState.folders.isEmpty)
        Divider()
        Button("删除", role: .destructive) { deleteItem(item) }
    }

    // MARK: - 操作

    private func batchDeleteItems() {
        for id in selectedItemIDs {
            if let item = listState.items.first(where: { $0.id == id }) {
                try? itemService.trashItem(item)
            }
        }
        selectedItemIDs.removeAll()
        isMultiSelectMode = false
        loadPage(listState.currentPage)
    }

    // MARK: - 分页逻辑

    /// 主动切页：重建 ScrollView 回顶部
    private func changePage(_ page: Int) {
        pendingAnchorID = nil
        scrollResetToken = UUID()
        loadPage(page)
    }

    /// 详情返回：保持当前页，恢复 anchor
    private func returnFromDetail() {
        pendingAnchorID = listState.anchorItemID
        loadPage(listState.currentPage, keepExistingData: true)
    }

    /// 首次进入平台：从第 1 页开始
    private func restoreBrowseState() {
        pendingAnchorID = nil
        scrollResetToken = UUID()
        loadPage(1)
    }

    private func saveBrowseState(anchorItemID: UUID? = nil) {
        var s = listState
        if let anchor = anchorItemID {
            s.anchorItemID = anchor
        }
        appState.platformListStates[stableID] = s
    }

    /// 删除后 reload 当前页：保留页码、从下一页补齐、保持位置
    private func reloadAfterDeletion() {
        let oldPage = listState.currentPage
        let oldItems = listState.items
        let pageSize = AppState.platformPageSize
        let newest = sortNewestFirst
        let itemRepo = appState.itemRepo
        let folderRepo = appState.folderRepo

        DispatchQueue.global(qos: .userInitiated).async {
            let count = (try? itemRepo.count(platform: platform)) ?? 0
            let pages = max(1, Int(ceil(Double(count) / Double(pageSize))))
            let targetPage = min(oldPage, pages)
            let offset = (targetPage - 1) * pageSize

            let loadedItems = (try? itemRepo.fetchPage(platform: platform, limit: pageSize, offset: offset)) ?? []
            let sortedItems = loadedItems.sorted {
                newest ? $0.importDate > $1.importDate : $0.importDate < $1.importDate
            }
            let loadedFolders = (try? folderRepo.fetchAll(platform: platform)) ?? []

            DispatchQueue.main.async {
                var s = self.listState
                s.totalCount = count
                s.totalPages = pages
                s.currentPage = targetPage
                s.items = sortedItems
                s.folders = loadedFolders
                s.isLoadingPage = false
                s.hasLoadedOnce = true

                // 恢复到删除位置附近
                let oldIndex = oldItems.firstIndex(where: { $0.id == self.appState.lastDeletedItemID }) ?? 0
                if oldIndex < sortedItems.count {
                    s.anchorItemID = sortedItems[oldIndex].id
                } else if oldIndex > 0, oldIndex - 1 < sortedItems.count {
                    s.anchorItemID = sortedItems[oldIndex - 1].id
                } else {
                    s.anchorItemID = sortedItems.first?.id
                }
                self.pendingAnchorID = s.anchorItemID
                self.appState.platformListStates[self.stableID] = s
            }
        }
    }

    private func loadPage(_ page: Int, keepExistingData: Bool = false) {
        let safePage = max(1, page)
        let pageSize = AppState.platformPageSize
        let offset = (safePage - 1) * pageSize
        let newest = sortNewestFirst
        let itemRepo = appState.itemRepo
        let folderRepo = appState.folderRepo

        // 只设置 loading 状态，不清空 items
        var s = listState
        s.isLoadingPage = true
        appState.platformListStates[stableID] = s

        DispatchQueue.global(qos: .userInitiated).async {
            let count = (try? itemRepo.count(platform: platform)) ?? 0
            let pages = max(1, Int(ceil(Double(count) / Double(pageSize))))
            let clampedPage = min(safePage, pages)
            let clampedOffset = (clampedPage - 1) * pageSize

            let loadedItems = (try? itemRepo.fetchPage(platform: platform, limit: pageSize, offset: clampedOffset)) ?? []
            let sortedItems = loadedItems.sorted {
                newest ? $0.importDate > $1.importDate : $0.importDate < $1.importDate
            }
            let loadedFolders = (try? folderRepo.fetchAll(platform: platform)) ?? []

            DispatchQueue.main.async {
                var s = self.listState
                s.totalCount = count
                s.totalPages = pages
                s.currentPage = clampedPage
                s.items = sortedItems
                s.folders = loadedFolders
                s.isLoadingPage = false
                s.hasLoadedOnce = true
                self.appState.platformListStates[self.stableID] = s
            }
        }
    }

    /// 仅用于删除/详情返回时恢复 anchor（不改变 scrollResetToken）
    private func restoreAnchorIfNeeded(proxy: ScrollViewProxy) {
        guard let anchorID = pendingAnchorID else { return }
        pendingAnchorID = nil
        proxy.scrollTo(anchorID, anchor: .center)
    }

    private func jumpToInputPage() {
        guard let page = Int(pageJumpText), page >= 1, page <= listState.totalPages, page != listState.currentPage else {
            pageJumpText = ""
            return
        }
        changePage(page)
        pageJumpText = ""
    }

    private func deleteItem(_ item: Item) {
        try? itemService.trashItem(item)
        loadPage(listState.currentPage)
    }
}

struct StatusTab: View {
    let title: String
    let isSelected: Bool
    let action: () -> Void
    var body: some View {
        Button(action: action) {
            Text(title)
                .font(.subheadline)
                .fontWeight(isSelected ? .semibold : .regular)
                .foregroundStyle(isSelected ? .primary : .secondary)
                .padding(.horizontal, 14)
                .padding(.vertical, 6)
                .background(isSelected ? Color.primary.opacity(0.1) : Color.clear)
                .clipShape(Capsule())
        }
        .buttonStyle(.plain)
    }
}

struct PlatformStatusView: View {
    let platform: Platform
    let status: ArchiveStatus
    var openDetail: (UUID) -> Void = { _ in }
    @Environment(AppState.self) private var appState
    @State private var items: [Item] = []
    var body: some View {
        Group {
            if items.isEmpty {
                VStack(spacing: 16) {
                    Image(systemName: status.iconName).font(.system(size: 48)).foregroundStyle(.tertiary)
                    Text("暂无\(status.displayName)内容").foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                List(items) { item in
                    Button { openDetail(item.id) } label: {
                        ItemListRow(item: item)
                    }
                    .buttonStyle(.plain)
                }.listStyle(.plain)
            }
        }
        .navigationTitle("\(platform.displayName) · \(status.displayName)")
        .onAppear { items = (try? appState.itemRepo.fetchAll(platform: platform, archiveStatus: status)) ?? [] }
    }
}
