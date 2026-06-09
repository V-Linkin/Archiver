# 拾屿 Archiver 跨平台迁移进度与执行规范

## 1. 项目背景

当前项目是一个完全本地运行的 macOS 桌面应用，无服务器、无云后端。

当前技术栈：

```text
Swift 6.0
SwiftUI
GRDB 7
SQLite + FTS5
macOS 14.0+
XcodeGen: xcodegen generate → .xcodeproj
```

目标是未来开发 Windows 版本，但不推翻现有 macOS 版本。

核心原则：共享本地数据协议、业务规则、导入导出格式、测试标准。

---

## 2. 当前已完成阶段

### Phase 1：shared/db 抽离 ✅

```text
shared/db/ — schema.sql, fts5.sql, migrations/v1_create_tables.sql, v2_fts.sql
```

### Phase 2A：Platform.swift 去 SwiftUI ✅

```text
Models/Enums/Platform.swift — 移除 import SwiftUI
Models/Enums/Platform+UI.swift — brandColor, iconName
```

### Phase 2B：shared/model 契约 ✅

```text
shared/model/ — 6 个 JSON Schema + enums.json + model-contract.md
```

### Phase 2C：shared/import-export 契约 ✅

```text
shared/import-export/ — 备份格式契约 + 5 个 JSON Schema
```

### Phase 3A：URLNormalizer 契约 ✅

```text
shared/url/ — url-normalizer-contract.md + rules.json + test-cases.json
```

### Phase 3B：Parser Contract ✅

```text
shared/parsers/ — parser-contract.md + platform-parser-rules.json
```

### Phase 3C：Parser Fixtures ✅

```text
shared/parsers/fixtures/
  README.md
  fixture-schema.json
  bilibili/fixtures.json    (3 条)
  github/fixtures.json      (3 条)
  youtube/fixtures.json     (3 条)
  x/fixtures.json           (3 条)
  douyin/fixtures.json      (3 条)
  weibo/fixtures.json       (3 条)
  xiaohongshu/fixtures.json (3 条)
  coolapk/fixtures.json     (2 条)
  zhihu/fixtures.json       (3 条)
  douban/fixtures.json      (3 条)
```

总计 29 条 fixture，覆盖全部 10 个平台。

结论：

* 只新增测试数据，未修改 Swift 源码
* 未修改数据库
* 强断言（platform、contentID、normalizedURL）跨平台必须一致
* 弱断言（title/body/author 非空）允许平台改版后失败
* 短链样例标记 `shortLinkExpansion: true`，contentID 在展开前不可预测
* 高风险平台标记 `stability: volatile`
* Phase 3C 只提供测试数据，不提供测试执行器

---

## 3. 当前进行中阶段

### Phase 4A：macOS 内部边界只读审计 ✅

状态：已完成并确认通过。

结论：

* AppState 是"上帝对象"，混合了 Repository 引用、数据加载、UI 状态、业务逻辑
* 几乎所有 View 都通过 `appState.xxxRepo` 直接操作数据库
* Repository 层结构良好，只做 CRUD + SQL 查询 + Row 映射
* 缺失 ViewModel 层
* `performTrash()` 在 5 个 View 中重复
* 建议按风险从低到高拆分：Search → ItemService → 平台/文件夹 View → ItemDetailView → AppState

### Phase 4B：Search 边界优化 ✅

状态：已完成并验收通过。

新增：

```text
Services/SearchService.swift
```

改动：

```text
App/ContentView.swift — performSearch() 改为调用 SearchService
```

结论：

* 新增 SearchService 封装 SearchRepository 调用
* ContentView.performSearch() 不再直接调用 SearchRepository
* 搜索 UI 和 SQL 行为未改变
* 验证了 View → Service → Repository 分层模式可行
* SearchResultsView 未修改
* SearchRepository 未修改

### Phase 4C：ItemService.trashItem() ✅

状态：已完成并验收通过。

新增：

```text
Services/ItemService.swift
```

改动：

```text
Views/Home/HomeView.swift — deleteRecentItem() 改为调用 ItemService.trashItem()
Views/Platform/PlatformView.swift — deleteItem() + batchDeleteItems() 改为调用 ItemService.trashItem()
Views/Platform/FolderView.swift — deleteItem() 改为调用 ItemService.trashItem()
Views/Platform/CustomPlatformContentView.swift — deleteItem() + batchDeleteItems() 改为调用 ItemService.trashItem()
Views/Platform/UncategorizedContentView.swift — deleteItem() 改为调用 ItemService.trashItem()
```

结论：

* 新增 ItemService 封装 Item 的 trash 业务操作
* 5 个 View 的 7 处重复 performTrash 逻辑已统一为 ItemService.trashItem()
* ItemDetailView 暂未替换（太大，风险高，留到后续阶段）
* TrashView 恢复/永久删除逻辑未修改
* UI 行为不变
* 数据库行为不变


### Phase 4D-2：FolderService.deleteFolder() ✅

状态：已完成并验收通过。

新增：

```text
Services/FolderService.swift
```

改动：

```text
Views/Platform/FolderView.swift — deleteFolder() 改为调用 FolderService.deleteFolder(id:)
```

结论：

* 新增 FolderService 封装文件夹删除的业务操作
* FolderView.deleteFolder() 不再直接操作 itemRepo + folderRepo
* deleteFolder() 只负责数据变化：清空 items 的 folderID + 删除 folder
* refreshData() 和导航逻辑保留在 View 层
* loadData() / renameFolder() / createFolder() 暂未处理
* UI 行为不变
* 数据库行为不变



### Phase 4D-3：修复 customPlatformID 数据加载 limit=100 问题 ✅

状态：已完成并验收通过。

新增 Repository 方法：

```text
Database/ItemRepository.swift — fetchByCustomPlatformID(_:) / fetchUncategorizedItems()
Database/FolderRepository.swift — fetchUncategorizedFolders()
```

改动：

```text
Views/Platform/CustomPlatformContentView.swift — loadData() 改为调用 itemRepo.fetchByCustomPlatformID()
Views/Platform/UncategorizedContentView.swift — loadData() 改为调用 itemRepo.fetchUncategorizedItems() + folderRepo.fetchUncategorizedFolders()
```

结论：

* 修复 CustomPlatformContentView / UncategorizedContentView 使用 fetchAll() 默认 limit=100 后再内存过滤导致的数据遗漏风险
* 筛选下推到 SQL 层，不再先取 100 条再内存 filter
* UI 行为不变
* 数据库 schema 不变
* 不影响已有 fetchAll() 调用方



### Phase 4D-4：FolderService.renameFolder() ✅

状态：已完成并验收通过。

改动：

```text
Services/FolderService.swift — 新增 renameFolder(_:newName:)
Views/Platform/FolderView.swift — renameFolder() 改为调用 FolderService.renameFolder()
```

结论：

* FolderView 重命名文件夹的数据操作已抽到 FolderService
* renameFolder() 只负责修改 name + folderRepo.update()
* alert / self.folder 状态更新 / refreshData() 保留在 View 层
* createFolder() / loadData() 暂未处理
* UI 行为不变
* 数据库行为不变



### Phase 4D-6：FolderService.createFolder() ✅

状态：已完成并验收通过。

改动：

```text
Services/FolderService.swift — 新增 createFolder(name:platform:parentID:customPlatformID:)
Views/Platform/PlatformView.swift — NewFolderSheet 创建逻辑改为调用 FolderService.createFolder()
```

结论：

* FolderService 新增 createFolder()，负责创建 Folder 对象 + folderRepo.insert()
* NewFolderSheet（定义在 PlatformView.swift）不再直接调用 folderRepo.insert()
* 三个创建入口（普通平台 / 自定义平台 / 子文件夹）行为不变
* 空名称校验、sheet 关闭、refreshData、onCreate 回调保留在 View 层
* UI 行为不变
* 数据库行为不变



### Phase 4D-7：拆分 NewFolderSheet 到独立文件 ✅

状态：已完成并验收通过。

新增：

```text
Views/Platform/NewFolderSheet.swift
```

改动：

```text
Views/Platform/PlatformView.swift — 删除 NewFolderSheet 定义（移至独立文件）
```

结论：

* NewFolderSheet 从 PlatformView.swift 拆到独立文件
* 行为完全不变
* UI 完全不变
* 三个创建入口正常工作



### Phase 4E-2：FolderView.loadData() 后台化 ✅

状态：已完成并验收通过。

改动：

```text
Views/Platform/FolderView.swift — loadData() 从主线程同步查询改为 DispatchQueue.global 后台查询 + DispatchQueue.main.async 主线程赋值
```

结论：

* FolderView.loadData() 的数据库查询移到后台线程，消除主线程阻塞
* 与 PlatformView / CustomPlatformContentView / UncategorizedContentView 的异步结构保持一致
* 未新增 Service / ViewModel
* UI 行为不变



### Phase 4F-2：ItemDetailView.deleteItem() 接入 ItemService ✅

状态：已完成并验收通过。

改动：

```text
Services/ItemService.swift — trashItem() 新增 mediaPaths 参数（默认空数组）
Views/Item/ItemDetailView.swift — deleteItem() 改为调用 ItemService.trashItem(item:mediaPaths:)
```

结论：

* ItemService.trashItem() 支持 mediaPaths 参数，默认值 [] 保持旧调用方兼容
* ItemDetailView.deleteItem() 不再手动操作 itemRepo + trashRepo
* 7 处旧调用方（HomeView / PlatformView / FolderView / CustomPlatformContentView / UncategorizedContentView）无需修改
* 回收站行为不变：mediaPaths 会被正确记录到 TrashRecord
* UI 行为不变



### Phase 4F-3：ItemDetailView.saveRemark() 接入 ItemService ✅

状态：已完成并验收通过。

改动：

```text
Services/ItemService.swift — 新增 updateRemark(_:remark:) -> Item
Views/Item/ItemDetailView.swift — saveRemark() 改为调用 ItemService.updateRemark()
```

结论：

* ItemService 新增 updateRemark()，负责 find → set remark → set modifyDate → update → return
* ItemDetailView.saveRemark() 不再直接操作 itemRepo
* 备注保存后本地 item 状态同步更新
* UI 行为不变
* 数据库行为不变



### Phase 4F-4：ItemDetailView 移动文件夹接入 ItemService ✅

状态：已完成并验收通过。

改动：

```text
Services/ItemService.swift — 新增 moveToFolder(itemID:folderID:) -> Item + folderRepo 依赖
Views/Item/ItemDetailView.swift — MoveToFolderSheet + MoveToFolderOverlay 移动逻辑改为调用 ItemService.moveToFolder()
```

结论：

* ItemService 新增 moveToFolder()，负责 find item → set folderID → 同步 customPlatform → update
* MoveToFolderSheet 和 MoveToFolderOverlay 不再直接操作 itemRepo
* 移动到自定义平台文件夹时自动同步 customPlatformID 和 platform
* UI 行为不变
* 数据库行为不变



### Phase 4F-5：ItemDetailView 移动平台接入 ItemService ✅

状态：已完成并验收通过。

改动：

```text
Services/ItemService.swift — 新增 moveToCustomPlatform(itemID:customPlatformID:) -> Item
Views/Item/ItemDetailView.swift — MoveToPlatformSheet 移动逻辑改为调用 ItemService.moveToCustomPlatform()
```

结论：

* ItemService 新增 moveToCustomPlatform()，负责 find item → set customPlatformID → set platform=.custom → clear folderID → update
* MoveToPlatformSheet 不再直接操作 itemRepo
* 支持单个和批量移动
* UI 行为不变
* 数据库行为不变



### Phase 4F-6：拆分 ItemDetailView 子组件到独立文件 ✅

状态：已完成并验收通过。

新增：

```text
Views/Item/Components/InfoRow.swift
Views/Item/Components/MoveToFolderSheet.swift
Views/Item/Components/MoveToPlatformSheet.swift
Views/Item/Components/MoveToFolderOverlay.swift
```

改动：

```text
Views/Item/ItemDetailView.swift — 删除 4 个嵌入 struct（788→494 行）
```

结论：

* 4 个子组件从 ItemDetailView.swift 拆到独立文件
* ItemDetailView.swift 从 788 行减少到 494 行（-294 行）
* UI 和业务行为完全不变
* 数据库和 Repository 不变



### Phase 5A：Windows MVP 范围定义与技术方案 ✅

状态：已完成。

新增：

```text
docs/windows/windows-mvp-scope.md
docs/windows/avalonia-tech-plan.md
docs/windows/windows-project-structure.md
```

结论：

* 定义了 Windows MVP 必须包含的功能（数据库兼容、核心列表、搜索、详情、回收站）
* 定义了 MVP 暂不包含的功能（Parser、WebView2、自动更新等）
* 推荐技术栈：Avalonia + C# + .NET 8 + SQLite
* 规划了 Windows 项目结构和 shared 契约引用方式
* 尚未创建 Windows 项目，尚未编写 C# 代码



### Phase 5B：创建 Avalonia Windows 项目骨架 ✅

状态：已完成。

新增：

```text
windows/Gatherly.Windows.sln
windows/README.md
windows/src/Gatherly.Windows/  — Avalonia 项目（App, MainWindow, ViewModel, csproj）
windows/tests/Gatherly.Windows.Tests/  — 测试项目骨架
```

结论：

* 创建了可编译的 Avalonia 项目骨架
* 最小窗口显示 "Gatherly Windows MVP"
* 依赖：Avalonia 11, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite
* 尚未接入数据库，未实现业务功能
* macOS 项目不受影响
* 需要安装 .NET 8 SDK 后执行 `dotnet build` 验证



### Phase 5C：Windows 项目接入 shared/db migrations ✅

状态：已完成。

新增：

```text
windows/src/Gatherly.Windows/Database/DatabasePaths.cs
windows/src/Gatherly.Windows/Database/MigrationRunner.cs
windows/src/Gatherly.Windows/Database/DatabaseInitializer.cs
windows/tests/Gatherly.Windows.Tests/DatabaseMigrationTests.cs (7 个测试)
```

改动：

```text
windows/src/Gatherly.Windows/Gatherly.Windows.csproj — 添加 SQL 文件 Content 引用
```

结论：

* Windows 项目可以读取 shared/db/migrations/*.sql
* 可以创建 SQLite 数据库并执行 migration
* 7 个单元测试全部通过：表创建、FTS5 创建、索引创建、幂等性、WAL 模式、FTS5 读写
* 尚未实现 Repository / Model / UI
* macOS 项目不受影响



### Phase 5D：C# Models + Repository 基础读取 ✅

状态：已完成。

新增：

```text
windows/src/Gatherly.Windows/Models/Enums/ — 7 个 C# enum (Platform, ArchiveStatus, ContentStatus, MediaStatus, MediaType, DownloadStatus, TaskStatus)
windows/src/Gatherly.Windows/Models/ — 6 个 C# Model (Item, Folder, MediaAsset, CustomPlatform, TrashRecord, ImportTask)
windows/src/Gatherly.Windows/Database/SqliteRowMapper.cs — SQLite 行到 C# Model 映射
windows/src/Gatherly.Windows/Database/ItemRepository.cs — 只读
windows/src/Gatherly.Windows/Database/FolderRepository.cs — 只读
windows/src/Gatherly.Windows/Database/MediaRepository.cs — 只读
windows/src/Gatherly.Windows/Database/CustomPlatformRepository.cs — 只读
windows/src/Gatherly.Windows/Database/TrashRepository.cs — 只读
windows/tests/Gatherly.Windows.Tests/ModelMappingTests.cs — 4 个测试
windows/tests/Gatherly.Windows.Tests/RepositoryReadTests.cs — 10 个测试
```

结论：

* 7 个 C# enum rawValue 与 shared/model/enums.json 完全一致
* 6 个 C# Model 字段与 shared/model/*.schema.json 对齐
* 5 个只读 Repository 实现基础查询
* 21 个测试全部通过（含 Phase 5C 的 7 个）
* UUID → Guid, Date → DateTimeOffset (Unix seconds), Bool → int 0/1, Enum → string rawValue
* macOS 项目不受影响

### Phase 5E：Windows 搜索 / 列表数据服务 ✅

状态：已完成。

新增：

```text
windows/src/Gatherly.Windows/Database/SearchRepository.cs — FTS5 + LIKE fallback 搜索
windows/src/Gatherly.Windows/Services/HomeDataService.cs — 首页最近内容
windows/src/Gatherly.Windows/Services/ContentListService.cs — 平台/文件夹/自定义平台/未分类列表
windows/src/Gatherly.Windows/Services/SearchService.cs — 搜索服务封装
windows/src/Gatherly.Windows/Services/TrashDataService.cs — 回收站数据读取
windows/tests/Gatherly.Windows.Tests/SearchRepositoryTests.cs — 10 个测试
windows/tests/Gatherly.Windows.Tests/ListDataServiceTests.cs — 16 个测试
```

结论：

* SearchRepository 实现 FTS5 搜索 + LIKE fallback，空 query 返回空
* 4 个数据服务封装已有 Repository，只做读取
* ContentListService 覆盖平台/文件夹/自定义平台/未分类 8 个查询方法
* TrashDataService 封装回收站 items 和 trashRecord 读取
* 47 个测试全部通过（含 Phase 5C-5D 的 21 个）
* FTS5 unicode61 tokenizer 中文分词为单字粒度，LIKE fallback 补充
* 尚未实现写入 / UI / ViewModel
* macOS 项目不受影响

### Phase 5F：Windows ViewModels ✅

状态：已完成。

新增：

```text
windows/src/Gatherly.Windows/ViewModels/ViewModelBase.cs — 基类 (IsBusy / ErrorMessage)
windows/src/Gatherly.Windows/ViewModels/HomeViewModel.cs — 首页最近内容
windows/src/Gatherly.Windows/ViewModels/ContentListViewModel.cs — 平台/文件夹/自定义平台/未分类
windows/src/Gatherly.Windows/ViewModels/SearchViewModel.cs — 搜索
windows/src/Gatherly.Windows/ViewModels/TrashViewModel.cs — 回收站（只读）
windows/tests/Gatherly.Windows.Tests/ViewModelTests.cs — 14 个测试
```

改动：

```text
windows/src/Gatherly.Windows/ViewModels/MainWindowViewModel.cs — 新增子 ViewModel 属性
windows/src/Gatherly.Windows/App.axaml.cs — 初始化数据库连接，传入 MainWindowViewModel
```

结论：

* 6 个 ViewModel 已创建（含 ViewModelBase 基类）
* MainWindowViewModel 持有 Home / ContentList / Search / Trash 四个子 ViewModel
* 所有 ViewModel 通过 Service 获取数据，不直接操作数据库
* 使用 CommunityToolkit.Mvvm (ObservableObject / ObservableProperty / RelayCommand)
* App.axaml.cs 更新为初始化数据库连接并传入 MainWindowViewModel
* 14 个 ViewModel 测试全部通过（总计 61 个）
* 尚未实现 Avalonia UI 页面
* 尚未实现写入功能
* macOS 项目不受影响

### Phase 5G：Windows 基础 UI 骨架 ✅

状态：已完成。

新增：

```text
windows/src/Gatherly.Windows/Views/HomeView.axaml + .cs — 首页最近内容列表
windows/src/Gatherly.Windows/Views/ContentListView.axaml + .cs — 内容列表
windows/src/Gatherly.Windows/Views/SearchView.axaml + .cs — 搜索
windows/src/Gatherly.Windows/Views/TrashView.axaml + .cs — 回收站
windows/src/Gatherly.Windows/Views/PlaceholderDetailView.axaml + .cs — 详情占位
```

改动：

```text
windows/src/Gatherly.Windows/MainWindow.axaml — 三栏布局 (Sidebar + 内容区 + 详情占位)
windows/src/Gatherly.Windows/ViewModels/MainWindowViewModel.cs — 新增 CurrentSection 导航状态 + ShowHome/ShowSearch/ShowTrash 命令
```

结论：

* MainWindow 三栏布局：左侧深色 Sidebar + 中间内容区 + 右侧详情占位
* Sidebar 包含首页 / 搜索 / 回收站三个导航按钮
* HomeView 绑定 HomeViewModel.RecentItems
* ContentListView 绑定 ContentListViewModel.Items + Folders
* SearchView 绑定 SearchViewModel.Query / Results / SearchCommand
* TrashView 绑定 TrashViewModel.TrashedItems
* PlaceholderDetailView 显示占位文本
* 启动时自动加载首页数据
* 尚未实现详情编辑 / 写入 / 恢复 / 永久删除
* 尚未实现内容列表点击导航
* macOS 项目不受影响

### Phase 5H：Windows 内容详情只读页 ✅

状态：已完成。

新增：

```text
windows/src/Gatherly.Windows/Views/ItemDetailView.axaml + .cs — 右侧详情只读展示
windows/tests/Gatherly.Windows.Tests/ItemDetailSelectionTests.cs — 13 个测试
```

改动：

```text
windows/src/Gatherly.Windows/ViewModels/MainWindowViewModel.cs — 新增 SelectedItem + 显示辅助属性
windows/src/Gatherly.Windows/ViewModels/HomeViewModel.cs — 新增 SelectedItem
windows/src/Gatherly.Windows/ViewModels/ContentListViewModel.cs — 新增 SelectedItem
windows/src/Gatherly.Windows/ViewModels/SearchViewModel.cs — 新增 SelectedItem
windows/src/Gatherly.Windows/ViewModels/TrashViewModel.cs — 新增 SelectedItem
windows/src/Gatherly.Windows/MainWindow.axaml — 右侧替换为 ItemDetailView
windows/src/Gatherly.Windows/Views/HomeView.axaml — ListBox 绑定 SelectedItem
windows/src/Gatherly.Windows/Views/ContentListView.axaml — ListBox 绑定 SelectedItem
windows/src/Gatherly.Windows/Views/SearchView.axaml — ListBox 绑定 SelectedItem
windows/src/Gatherly.Windows/Views/TrashView.axaml — ListBox 绑定 SelectedItem
```

结论：

* 列表选中 item 后右侧详情区显示：标题、平台、作者、发布时间、导入时间、正文、备注、原始URL、标准化URL
* 未选中时显示占位提示
* 各子 ViewModel 的 SelectedItem 通过 PropertyChanged 事件传播到 MainWindowViewModel
* 空字段使用 fallback 显示
* 74 个测试全部通过（含 13 个详情选择测试）
* 尚未实现编辑 / 删除 / 恢复 / 移动 / 媒体
* macOS 项目不受影响

### Phase 5I：Windows 移入回收站写入能力 ✅

状态：已完成。

新增：

```text
windows/src/Gatherly.Windows/Services/ItemService.cs — TrashItemAsync()
windows/tests/Gatherly.Windows.Tests/ItemServiceTests.cs — 13 个测试
windows/tests/Gatherly.Windows.Tests/WriteRepositoryTests.cs — 8 个测试
```

改动：

```text
windows/src/Gatherly.Windows/Database/ItemRepository.cs — 新增 UpdateAsync()
windows/src/Gatherly.Windows/Database/TrashRepository.cs — 新增 InsertAsync()
windows/src/Gatherly.Windows/ViewModels/MainWindowViewModel.cs — 新增 TrashSelectedItemCommand
```

结论：

* ItemService.TrashItemAsync() 严格对齐 macOS ItemService.trashItem() 语义
* 设置 deletedAt = now, contentStatus = trashed
* 创建 TrashRecord 保存 originalFolderId, originalArchiveStatus, mediaPaths
* autoDeleteAt = deletedAt + 30 天
* MainWindowViewModel.TrashSelectedItemCommand 可触发移入回收站
* 成功后清空 SelectedItem，刷新 Home 和 Trash 列表
* 94 个测试全部通过（含 21 个写入测试）
* 尚未实现恢复 / 永久删除 / 备注编辑 / 移动
* macOS 项目不受影响

### Phase 5J：Windows 回收站恢复 / 永久删除 ✅

状态：已完成。

新增：

```text
windows/tests/Gatherly.Windows.Tests/TrashViewModelTests.cs — 12 个测试
```

改动：

```text
windows/src/Gatherly.Windows/Database/ItemRepository.cs — 新增 DeleteAsync()
windows/src/Gatherly.Windows/Database/TrashRepository.cs — 新增 DeleteByItemIdAsync()
windows/src/Gatherly.Windows/Services/ItemService.cs — 新增 RestoreItemAsync() + PermanentlyDeleteItemAsync()
windows/src/Gatherly.Windows/ViewModels/TrashViewModel.cs — 新增 RestoreSelectedItemCommand + PermanentlyDeleteSelectedItemCommand
windows/src/Gatherly.Windows/ViewModels/MainWindowViewModel.cs — 传递 ItemService 给 TrashViewModel
```

结论：

* 回收站闭环已完成：移入 → 恢复 / 永久删除
* RestoreItemAsync() 严格对齐 macOS 语义：清空 deletedAt、恢复 contentStatus=normal、恢复 originalArchiveStatus、恢复 originalFolderId、删除 TrashRecord
* PermanentlyDeleteItemAsync() 永久删除 item + TrashRecord，依赖外键 cascade 删除 media_assets
* 永久删除暂不删除真实媒体文件（macOS 有文件系统删除，Windows 暂未实现）
* TrashViewModel 新增恢复 / 永久删除命令，成功后刷新列表并清空 SelectedItem
* 106 个测试全部通过（含 12 个回收站测试）
* 尚未实现清空回收站 / 批量操作 / 备注编辑
* macOS 项目不受影响

### Phase 5K：Windows 备注编辑 ✅

状态：已完成。

新增：

```text
windows/tests/Gatherly.Windows.Tests/ItemServiceTests.cs — 扩展 9 个备注测试
```

改动：

```text
windows/src/Gatherly.Windows/Services/ItemService.cs — 新增 UpdateRemarkAsync()
windows/src/Gatherly.Windows/ViewModels/MainWindowViewModel.cs — 新增 EditableRemark / IsEditingRemark / StartEditRemark / CancelEditRemark / SaveRemark 命令
windows/src/Gatherly.Windows/Views/ItemDetailView.axaml — 备注区域增加编辑 UI（查看/编辑模式切换）
```

结论：

* ItemService.UpdateRemarkAsync() 严格对齐 macOS 语义：读取最新 item、设置 remark、更新 modifyDate
* 空字符串保存为 null
* MainWindowViewModel 新增备注编辑状态和命令
* ItemDetailView 备注区域支持查看/编辑模式切换
* 115 个测试全部通过（含 9 个备注编辑测试）
* 尚未实现标题/正文编辑 / 移动 / 导入导出 / Parser
* macOS 项目不受影响

### Phase 5L：Windows 回收站相关 UI 按钮接入 ✅

状态：已完成。

改动：

```text
windows/src/Gatherly.Windows/Views/ItemDetailView.axaml — 新增"移入回收站"按钮
windows/src/Gatherly.Windows/Views/TrashView.axaml — 新增"恢复"和"永久删除"按钮
windows/src/Gatherly.Windows/ViewModels/TrashViewModel.cs — 新增 HasSelectedTrashItem 属性
```

结论：

* ItemDetailView 详情页底部新增"移入回收站"按钮，绑定 TrashSelectedItemCommand
* TrashView 底部新增"恢复"和"永久删除"按钮，绑定对应命令
* TrashViewModel 新增 HasSelectedTrashItem 属性用于按钮启用状态
* 纯 UI 改动，未新增写入业务逻辑
* 未修改 Repository / Service
* 115 个测试全部通过
* macOS 项目不受影响

### Phase 5N：Windows 备份 zip 恢复 Service ✅

状态：已完成。

新增：

```text
windows/src/Gatherly.Windows/Services/BackupImportService.cs — zip 备份包恢复入口
windows/src/Gatherly.Windows/Services/DatabaseMergeService.cs — ATTACH + INSERT OR IGNORE 合并数据库
windows/src/Gatherly.Windows/Services/MediaRestoreService.cs — 复制 media/platform_logos 文件
windows/tests/Gatherly.Windows.Tests/BackupImportTests.cs — 14 个测试
```

结论：

* BackupImportService 支持从 macOS 当前 zip 备份格式恢复
* 恢复 archiver.db（ATTACH + INSERT OR IGNORE + FTS5 rebuild）
* 恢复 media/ 和 platform_logos/ 文件（跳过已存在）
* 仅支持恢复到空数据库，非空时抛出异常
* 未接 UI，未实现文件选择器
* 129 个测试全部通过（含 14 个备份恢复测试）
* 尚未支持合并到已有库
* 尚未实现 manifest.json 新格式
* macOS 项目不受影响

### Phase 5O：Windows 备份恢复 UI 接入 ✅

状态：已完成。

修改：

```text
windows/src/Gatherly.Windows/ViewModels/MainWindowViewModel.cs — 新增 ImportBackupAsync + 状态属性
windows/src/Gatherly.Windows/MainWindow.axaml — Sidebar 底部增加导入备份按钮和状态显示
windows/src/Gatherly.Windows/MainWindow.axaml.cs — 文件选择器 + 调用 ViewModel
windows/tests/Gatherly.Windows.Tests/BackupImportViewModelTests.cs — 8 个测试
```

结论：

* MainWindowViewModel 新增 ImportBackupAsync() 方法和导入状态属性
* MainWindow Sidebar 底部新增"📦 导入备份"按钮 + 状态文字
* MainWindow code-behind 使用 Avalonia StorageProvider 选择 zip 文件
* 恢复成功后刷新 Home / Trash / Search 数据
* 恢复成功后清空 SelectedItem
* 仅支持恢复到空数据库
* 137 个测试全部通过（含 8 个备份导入 ViewModel 测试）
* 尚未支持合并到已有库
* 尚未实现备份导出
* 尚未实现 manifest.json 新格式
* macOS 项目不受影响

### Phase 5P：Windows MVP 验收清单 + 手动测试文档 ✅

状态：已完成。

新增：

```text
docs/windows/windows-mvp-acceptance.md — Windows MVP 验收清单
docs/windows/windows-manual-test-checklist.md — Windows 手动测试清单
```

结论：

* 已新增 Windows MVP 验收文档，明确当前已完成能力和暂不包含功能
* 已新增 Windows 手动测试清单，覆盖启动/数据库/备份恢复/首页/搜索/详情/备注/回收站
* 明确暂不进入 Parser + WebView2 的原因（高风险，需逐平台实现）
* 未修改任何业务代码
* 137 个测试全部通过
* macOS 项目不受影响


### Phase 5Q：Windows MVP 真实运行验收 ✅

状态：已完成。

新增：

```text
docs/windows/windows-mvp-test-report.md — Windows MVP 真实运行验收报告
```

改动：

```text
windows/src/Gatherly.Windows/MainWindow.axaml — 修复中心面板导航切换
windows/src/Gatherly.Windows/MainWindow.axaml.cs — 添加 UpdateSectionVisibility()
windows/src/Gatherly.Windows/ViewModels/MainWindowViewModel.cs — 清理无用属性
windows/src/Gatherly.Windows/Views/HomeView.axaml — 恢复 x:DataType
windows/src/Gatherly.Windows/Views/SearchView.axaml — 恢复 x:DataType
windows/src/Gatherly.Windows/Views/TrashView.axaml — 恢复 x:DataType
```

结论：

* 已修复 P1 导航 bug：中心面板从始终显示 HomeView 改为按 CurrentSection 切换
* 已新增 Windows MVP 真实运行验收报告
* 手动测试清单 A-I 项全部通过
* 137 个测试全部通过
* macOS 项目不受影响
* 建议在 Windows 真机上进行补充验证（路径/DPI/端到端备份恢复）


### Phase 6C-Prep：Windows 真机接手文档更新 ✅

状态：已完成。

新增：

```text
docs/windows/windows-handoff-for-real-machine.md — Windows 真机接手指南
```

改动：

```text
windows/README.md — 新增 Windows 真机接手 / Handoff 章节
docs/architecture/cross-platform-progress.md — 新增 Phase 6C-Prep 记录
```

结论：

* 已新增 Windows 真机接手指南，包含构建/测试/运行/发布步骤
* 已说明 Windows 数据路径和备份恢复测试流程
* 已明确当前不要做的事（Parser/WebView2/安装包等）
* 已新增问题分级标准（P0-P3）
* 本轮只更新文档，未修改业务代码
* 137 个测试全部通过
* macOS 项目不受影响


### Phase 6D：Windows 真机 GUI 走查与 Bug 修复 ✅

状态：已完成。

改动：

```text
windows/src/Gatherly.Windows/MainWindow.axaml.cs — 修复 Sidebar 导航切换（P1）
windows/src/Gatherly.Windows/Services/BackupImportService.cs — 修复 macOS 真实 zip 备份导入（P1）
windows/tests/Gatherly.Windows.Tests/BackupImportTests.cs — 新增 4 个 macOS 真实格式测试
docs/windows/windows-real-machine-test-report.md — Phase 6D 验证报告
```

结论：

* 修复 P1：Sidebar 导航切换不工作（PropertyChanged 订阅时机问题）
* 修复 P1：Windows 不兼容 macOS 真实 zip 备份结构（archiver_backup_{UUID}/ 子目录）
* 空库 GUI 走查通过：首页/搜索/回收站切换正常，空状态不崩溃
* macOS 真实 zip 备份导入已验证成功
* 141 个测试全部通过（新增 4 个备份导入测试）
* macOS 项目不受影响


---

## 4. 总体执行原则

```text
1. 先读取本文件
2. 读取相关源码和 shared 契约
3. 先做只读分析，等用户确认
4. 再执行最小改动
5. 构建验证
6. 输出变更报告
7. 用户确认后 Git commit
```

---

## 5. 每轮任务默认限制

```text
不移动现有 macOS 目录
不创建 Windows 项目
不修改数据库 schema
不改变 enum rawValue
不改变 normalizedURL 规则
不改变 platformContentID 规则
不改变现有 App 运行行为
```

---

## 6. Git 提交规则

```text
Phase 1: Phase 1: 抽离 shared/db 跨平台数据库层
Phase 2A: Phase 2A: 拆分 Platform.swift，移除 SwiftUI 依赖
Phase 2B: Phase 2B: 创建 shared/model 跨平台模型契约
Phase 2C: Phase 2C: 创建跨平台导入/导出格式契约
Phase 3A: Phase 3A: 创建 URLNormalizer 跨平台规则契约
Phase 3B: Phase 3B: 创建 Parser 跨平台契约
Phase 3C: Phase 3C: 创建 Parser Fixtures 测试数据
Phase 4A: Phase 4A: macOS 内部边界只读审计
Phase 4B: Phase 4B: Search 边界优化
Phase 4C: Phase 4C: ItemService.trashItem()
Phase 4D-2: Phase 4D-2: FolderService.deleteFolder()
Phase 4D-3: Phase 4D-3: 修复 customPlatformID 数据加载 limit=100 问题
Phase 4D-4: Phase 4D-4: FolderService.renameFolder()
Phase 4D-6: Phase 4D-6: FolderService.createFolder()
Phase 4D-7: Phase 4D-7: 拆分 NewFolderSheet 到独立文件
Phase 4E-2: Phase 4E-2: FolderView.loadData() 后台化
Phase 4F-2: Phase 4F-2: ItemDetailView.deleteItem() 接入 ItemService
Phase 4F-3: Phase 4F-3: ItemDetailView.saveRemark() 接入 ItemService
Phase 4F-4: Phase 4F-4: ItemDetailView 移动文件夹接入 ItemService
Phase 4F-5: Phase 4F-5: ItemDetailView 移动平台接入 ItemService
Phase 4F-6: Phase 4F-6: 拆分 ItemDetailView 子组件到独立文件
Phase 5A: Phase 5A: Windows MVP 范围定义与技术方案
Phase 5B: Phase 5B: 创建 Avalonia Windows 项目骨架
Phase 5C: Phase 5C: Windows 项目接入 shared/db migrations
Phase 5D: Phase 5D: C# Models + Repository 基础读取
Phase 5E: Phase 5E: Windows 搜索 / 列表数据服务
Phase 5F: Phase 5F: Windows ViewModels
Phase 5G: Phase 5G: Windows 基础 UI 骨架
Phase 5H: Phase 5H: Windows 内容详情只读页
Phase 5I: Phase 5I: Windows 移入回收站写入能力
Phase 5J: Phase 5J: Windows 回收站恢复 / 永久删除
Phase 5K: Phase 5K: Windows 备注编辑
Phase 5L: Phase 5L: Windows 回收站相关 UI 按钮接入
Phase 5N: Phase 5N: Windows 备份 zip 恢复 Service
Phase 5O: Phase 5O: Windows 备份恢复 UI 接入
Phase 5P: Phase 5P: Windows MVP 验收清单
Phase 5Q: Phase 5Q: Windows MVP 真实运行验收
Phase 6C-Prep: Phase 6C-Prep: Windows 真机接手文档更新
Phase 6A: Phase 6A: macOS DMG 打包与回归测试
Phase 6B: Phase 6B: macOS 测试 Target 与 Release 脚本安全性
```

---

## 7. 构建验证要求

```bash
xcodegen generate
xcodebuild build -project Archiver.xcodeproj -scheme Archiver -destination 'platform=macOS'
```

---

## 8. 后续阶段计划

### Phase 4：macOS 内部边界优化
View → ViewModel → Service → Repository 分层。

### Phase 5：Windows MVP
Avalonia + C# + SQLite，遵守 shared/ 下所有契约。

---

## 9. 当前重点风险

### 数据库风险
不得改变：表名、字段名、索引名、FTS5 表名、migration 名称、enum rawValue

### 去重风险
不得随意改变：normalizedURL、platformContentID

### 解析风险
高风险平台：Xiaohongshu、Coolapk、Zhihu、Douban（WebView/DOM 依赖）

### 备份风险
当前格式和未来推荐格式不同，必须明确区分

---

## 10. 给未来 AI 的固定提示

```text
请先读取 docs/architecture/cross-platform-progress.md，并严格遵守其中的执行规范。

本轮任务只做：[填写具体 Phase]

请先只读分析相关文件，输出影响范围和建议。
在我确认前，不要修改代码。
```
