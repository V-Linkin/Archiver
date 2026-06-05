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
