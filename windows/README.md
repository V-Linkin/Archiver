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

❌ Phase 5J: 回收站恢复 / 永久删除
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
| `ItemRepository` | Items 读取（7 个方法）+ UpdateAsync |
| `FolderRepository` | Folders 只读查询（5 个方法） |
| `MediaRepository` | MediaAssets 只读查询（1 个方法） |
| `CustomPlatformRepository` | CustomPlatforms 只读查询（2 个方法） |
| `TrashRepository` | TrashRecords 读取（2 个方法）+ InsertAsync |
| `SearchRepository` | FTS5 + LIKE fallback 搜索 |

### 服务层（Services）

| 类 | 职责 |
|---|---|
| `HomeDataService` | 首页最近内容 |
| `ContentListService` | 平台 / 文件夹 / 自定义平台 / 未分类列表 |
| `SearchService` | 搜索服务封装 |
| `TrashDataService` | 回收站数据读取 |
| `ItemService` | Item 业务操作（TrashItemAsync） |

### ViewModel 层

| 类 | 职责 |
|---|---|
| `ViewModelBase` | 基类（IsBusy / ErrorMessage） |
| `HomeViewModel` | 首页最近内容列表 + SelectedItem |
| `ContentListViewModel` | 平台 / 文件夹 / 自定义平台 / 未分类 + SelectedItem |
| `SearchViewModel` | 搜索 + SelectedItem |
| `TrashViewModel` | 回收站（只读）+ SelectedItem |
| `MainWindowViewModel` | 根容器 + 导航 + SelectedItem + TrashSelectedItemCommand |

### UI 层

| View | 说明 |
|---|---|
| `MainWindow` | 三栏布局：Sidebar + 内容区 + 详情区 |
| `HomeView` | 首页最近内容列表（支持选中） |
| `ContentListView` | 内容列表（支持选中） |
| `SearchView` | 搜索输入 + 结果列表（支持选中） |
| `TrashView` | 回收站列表（支持选中） |
| `ItemDetailView` | 右侧详情只读展示 |

### 测试

```text
dotnet test windows/Gatherly.Windows.sln
```

94 个测试全部通过。

## 技术栈

```text
UI:       Avalonia 11
Language: C# 12+
Runtime:  .NET 8
Database: SQLite (Microsoft.Data.Sqlite)
MVVM:     CommunityToolkit.Mvvm
```

## 本地构建

```bash
dotnet build windows/Gatherly.Windows.sln
dotnet run --project windows/src/Gatherly.Windows
```

## 注意

- 本项目与 macOS 版共享 `shared/` 契约
- 数据库文件 (.db) 跨平台兼容
- 当前实现：数据读取 + ViewModel + 三栏 UI + 详情只读 + 移入回收站
- 尚未实现恢复 / 永久删除 / 备注编辑 / 导入导出
