# Gatherly Windows (MVP)

## 当前状态

```text
✅ Phase 5B: Avalonia 项目骨架
✅ Phase 5C: 数据库初始化 + Migration 执行
✅ Phase 5D: C# Models + 只读 Repository
✅ Phase 5E: 搜索 + 列表数据服务
✅ Phase 5F: Windows ViewModels
✅ Phase 5G: Windows 基础 UI 骨架
✅ Phase 5H: Windows 内容详情只读页
✅ Phase 5I: Windows 移入回收站写入能力
✅ Phase 5J: Windows 回收站恢复 / 永久删除
✅ Phase 5K: Windows 备注编辑
✅ Phase 5L: Windows 回收站相关 UI 按钮接入
✅ Phase 5N: Windows 备份 zip 恢复 Service

✅ Phase 5O: Windows 备份恢复 UI 接入
✅ Phase 5P: Windows MVP 验收清单
✅ Phase 5Q: Windows MVP 真实运行验收
❌ Phase 6:  Parser + WebView2
❌ Phase 7:  安装包
```

## 已实现能力

### 数据层（Database）

| 类 | 职责 |
|---|---|
| `DatabaseInitializer` | 创建/打开 SQLite，执行 migration |
| `MigrationRunner` | 读取 shared/db/migrations/*.sql |
| `DatabasePaths` | 跨平台数据库路径 |
| `SqliteRowMapper` | SQLite 行 → C# Model 映射 |
| `ItemRepository` | Items 读写（9 个方法） |
| `FolderRepository` | Folders 只读查询（5 个方法） |
| `MediaRepository` | MediaAssets 只读查询（1 个方法） |
| `CustomPlatformRepository` | CustomPlatforms 只读查询（2 个方法） |
| `TrashRepository` | TrashRecords 读写（4 个方法） |
| `SearchRepository` | FTS5 + LIKE fallback 搜索 |

### 服务层（Services）

| 类 | 职责 |
|---|---|
| `HomeDataService` | 首页最近内容 |
| `ContentListService` | 平台 / 文件夹 / 自定义平台 / 未分类列表 |
| `SearchService` | 搜索服务封装 |
| `TrashDataService` | 回收站数据读取 |
| `ItemService` | Item 业务操作（Trash / Restore / Delete / UpdateRemark） |
| `BackupImportService` | zip 备份包恢复（解压 → 合并数据库 → 恢复 media） |
| `DatabaseMergeService` | ATTACH + INSERT OR IGNORE 合并数据库 |
| `MediaRestoreService` | 复制 media/platform_logos 文件 |

### ViewModel 层

| 类 | 职责 |
|---|---|
| `ViewModelBase` | 基类（IsBusy / ErrorMessage） |
| `HomeViewModel` | 首页最近内容列表 |
| `ContentListViewModel` | 平台 / 文件夹 / 自定义平台 / 未分类 |
| `SearchViewModel` | 搜索 |
| `TrashViewModel` | 回收站 + Restore + PermanentDelete |
| `MainWindowViewModel` | 根容器 + 导航 + 备注编辑 + TrashSelectedItem |

### UI 层

| View | 说明 |
|---|---|
| `MainWindow` | 三栏布局 |
| `HomeView` | 首页最近内容列表 |
| `ContentListView` | 内容列表 |
| `SearchView` | 搜索 + 结果列表 |
| `TrashView` | 回收站列表 + 恢复/永久删除按钮 |
| `ItemDetailView` | 详情 + 备注编辑 + 移入回收站按钮 |

### 测试

```text
dotnet test windows/Gatherly.Windows.sln
```

129 个测试全部通过。

## 技术栈

```text
UI:       Avalonia 11
Language: C# 12+
Runtime:  .NET 8
Database: SQLite (Microsoft.Data.Sqlite)
MVVM:     CommunityToolkit.Mvvm
```

## 备份恢复

支持从 macOS 当前 zip 备份格式恢复：

```csharp
var service = new BackupImportService();
await service.ImportBackupAsync(zipPath, dbPath, dataDir);
```

- 恢复 `archiver.db`（ATTACH + INSERT OR IGNORE）
- 恢复 `media/` 文件
- 恢复 `platform_logos/` 文件
- FTS5 索引 rebuild
- 仅支持恢复到空数据库

## 本地构建

```bash
dotnet build windows/Gatherly.Windows.sln
dotnet run --project windows/src/Gatherly.Windows
```

## 注意

- 本项目与 macOS 版共享 `shared/` 契约
- 数据库文件 (.db) 跨平台兼容
- 已实现：列表/搜索/详情/回收站/备注编辑/备份恢复 Service
- 尚未实现：Parser / WebView2 / 安装包
| `MainWindowViewModel` | 根容器 + 导航 + 备注编辑 + TrashSelectedItem + 备份导入 |
| `MainWindow` | 三栏布局 + Sidebar 导入备份按钮 + 文件选择器 |
137 个测试全部通过。
- 验收文档：docs/windows/windows-mvp-acceptance.md
- 手动测试清单：docs/windows/windows-manual-test-checklist.md
- 真实运行验收报告：docs/windows/windows-mvp-test-report.md
