# Gatherly Windows (MVP)

## 当前状态

```text
✅ Phase 5B: Avalonia 项目骨架
✅ Phase 5C: 数据库初始化 + Migration 执行
✅ Phase 5D: C# Models + 只读 Repository
✅ Phase 5E: 搜索 + 列表数据服务
✅ Phase 5F: Windows ViewModels
✅ Phase 5G: Windows 基础 UI 骨架

❌ Phase 5H: 内容详情只读页
❌ Phase 5I: 基础导入 / 备份恢复
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
| `ItemRepository` | Items 只读查询（7 个方法） |
| `FolderRepository` | Folders 只读查询（5 个方法） |
| `MediaRepository` | MediaAssets 只读查询（1 个方法） |
| `CustomPlatformRepository` | CustomPlatforms 只读查询（2 个方法） |
| `TrashRepository` | TrashRecords 只读查询（2 个方法） |
| `SearchRepository` | FTS5 + LIKE fallback 搜索 |

### 服务层（Services）

| 类 | 职责 |
|---|---|
| `HomeDataService` | 首页最近内容 |
| `ContentListService` | 平台 / 文件夹 / 自定义平台 / 未分类列表 |
| `SearchService` | 搜索服务封装 |
| `TrashDataService` | 回收站数据读取 |

### ViewModel 层

| 类 | 职责 |
|---|---|
| `ViewModelBase` | 基类（IsBusy / ErrorMessage） |
| `HomeViewModel` | 首页最近内容列表 |
| `ContentListViewModel` | 平台 / 文件夹 / 自定义平台 / 未分类 |
| `SearchViewModel` | 搜索 |
| `TrashViewModel` | 回收站（只读） |
| `MainWindowViewModel` | 根容器 + 导航状态 + 子 ViewModel |

### UI 层

| View | 说明 |
|---|---|
| `MainWindow` | 三栏布局：Sidebar + 内容区 + 详情占位 |
| `HomeView` | 首页最近内容列表 |
| `ContentListView` | 内容列表（items + folders） |
| `SearchView` | 搜索输入 + 结果列表 |
| `TrashView` | 回收站列表 |
| `PlaceholderDetailView` | 右侧详情占位 |

### 测试

```text
dotnet test windows/Gatherly.Windows.sln
```

61 个测试全部通过。

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

需要安装 .NET 8 SDK：

```bash
# macOS
brew install dotnet@8

# Windows
# https://dotnet.microsoft.com/download/dotnet/8.0
```

## 搜索说明

- FTS5 `unicode61` tokenizer 对中文分词为单字粒度
- 英文搜索正常
- LIKE fallback 支持中文子串匹配

## 注意

- 本项目与 macOS 版共享 `shared/` 契约
- 数据库文件 (.db) 跨平台兼容
- 当前只实现数据读取 + ViewModel + 基础 UI
- 尚未实现详情编辑 / 写入 / 导入导出
