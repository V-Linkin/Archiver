# Windows MVP 验收清单

## 1. 当前 MVP 状态

Windows MVP 已完成以下核心能力：

| # | 能力 | 状态 | Phase |
|---|------|------|-------|
| 1 | SQLite 数据库创建与初始化 | ✅ | 5C |
| 2 | shared/db migrations 执行 | ✅ | 5C |
| 3 | C# Models / Enums（7 个枚举） | ✅ | 5D |
| 4 | Repository 读取（Item/Folder/Media/CustomPlatform/Trash/Search） | ✅ | 5D |
| 5 | Repository 写入（Item/Trash） | ✅ | 5I |
| 6 | 数据 Service（Home/ContentList/Search/Trash） | ✅ | 5E |
| 7 | ViewModel 层（Home/ContentList/Search/Trash/MainWindow） | ✅ | 5F |
| 8 | MainWindow 三栏 UI 布局 | ✅ | 5G |
| 9 | 首页最近内容展示 | ✅ | 5G |
| 10 | 关键词搜索 | ✅ | 5E |
| 11 | 内容列表展示 | ✅ | 5G |
| 12 | 内容详情只读展示 | ✅ | 5H |
| 13 | 备注编辑 | ✅ | 5K |
| 14 | 移入回收站 | ✅ | 5I |
| 15 | 回收站恢复 | ✅ | 5J |
| 16 | 永久删除 | ✅ | 5J |
| 17 | 回收站 UI 按钮（恢复/永久删除） | ✅ | 5L |
| 18 | macOS zip 备份恢复 Service | ✅ | 5N |
| 19 | macOS zip 备份恢复 UI 入口 | ✅ | 5O |
| 20 | 单元测试 137 个通过 | ✅ | 5B–5O |

### 技术栈

```text
UI:       Avalonia 11.1
Language: C# 12
Runtime:  .NET 8
Database: SQLite (Microsoft.Data.Sqlite 8.0)
MVVM:     CommunityToolkit.Mvvm 8.2
```

### 项目结构

```text
windows/
  src/Gatherly.Windows/
    Database/     — 10 个文件（Initializer, MigrationRunner, Repositories, RowMapper）
    Models/       — 6 个 Model + 7 个 Enum
    Services/     — 8 个 Service
    ViewModels/   — 6 个 ViewModel
    Views/        — 6 个 View + 1 个 Placeholder
  tests/Gatherly.Windows.Tests/
    12 个测试文件，137 个测试用例
```

---

## 2. MVP 暂不包含

| 功能 | 原因 | 建议阶段 |
|------|------|---------|
| Parser（网页内容抓取） | 需逐平台实现，涉及 HTTP/WebView2/反爬 | Phase 6 |
| WebView2 解析 | 依赖 Windows WebView2 SDK，高风险 | Phase 6 |
| 自动抓取 | 依赖 Parser + WebView2 | Phase 6 |
| 高级媒体预览 | 需要媒体播放组件 | Phase 6 |
| 备份导出 | 仅实现了恢复，未实现导出 | Phase 6 |
| 安装包 | 需要 WiX/Inno Setup 等打包工具 | Phase 7 |
| 自动更新 | 需要安装包 + 更新服务 | Phase 7 |
| 完整设置页 | 仅实现了数据目录路径硬编码 | Phase 7 |
| 批量操作 | 批量删除/移动/导出 | Phase 7 |
| 新建内容 | 手动新建 item | Phase 7 |
| 标题/正文编辑 | 仅实现了备注编辑 | Phase 7 |
| 移动文件夹/移动平台 | 未实现 | Phase 7 |
| 全局快捷键 | Windows 专属实现 | Phase 7 |
| 文件关联 | Windows 注册表操作 | Phase 7 |
| DPI 缩放适配 | 需 Windows 真机测试 | Phase 6 |

---

## 3. 为什么暂不进入 Parser + WebView2

Parser + WebView2 是 Windows MVP 中风险最高的部分：

1. **逐平台实现**：每个平台（Bilibili、YouTube、X、小红书等）的网页结构不同，需要逐个适配
2. **登录态依赖**：部分平台内容需要登录才能访问，涉及 Cookie/Session 管理
3. **反爬机制**：平台会更新反爬策略，导致 Parser 频繁失效
4. **JS 注入风险**：WebView2 需要注入 JavaScript 提取内容，安全性和稳定性难以保证
5. **macOS 已有 Parser**：当前 macOS 端的 Parser 是基于 Swift + WKWebView 实现的，Windows 端需要用 C# + WebView2 重新实现
6. **验证优先级更高**：当前更重要的是确认数据迁移（macOS → Windows）和核心本地功能（列表/搜索/详情/回收站/备注/备份恢复）稳定

**建议**：在 Windows MVP 验收稳定后，再单独进入 Phase 6 处理 Parser + WebView2。

---

## 4. 当前测试状态

```text
dotnet test windows/Gatherly.Windows.sln
已通过! - 失败: 0, 通过: 137, 已跳过: 0, 总计: 137
```

```text
xcodebuild build -project Archiver.xcodeproj -scheme Archiver -destination 'platform=macOS'
BUILD SUCCEEDED
```

### 测试覆盖

| 测试文件 | 测试数 | 覆盖范围 |
|---------|--------|---------|
| DatabaseMigrationTests.cs | — | 数据库 migration 执行 |
| ModelMappingTests.cs | — | SQLite 行 → C# Model 映射 |
| RepositoryReadTests.cs | — | Repository 读取方法 |
| SearchRepositoryTests.cs | — | FTS5 搜索 + LIKE fallback |
| ListDataServiceTests.cs | — | 列表数据服务 |
| ViewModelTests.cs | — | 各 ViewModel 基本功能 |
| ItemDetailSelectionTests.cs | — | 详情选择状态 |
| ItemServiceTests.cs | — | Trash/Restore/Delete/UpdateRemark |
| WriteRepositoryTests.cs | — | Repository 写入方法 |
| TrashViewModelTests.cs | — | 回收站 ViewModel |
| BackupImportTests.cs | — | 备份恢复 Service |
| BackupImportViewModelTests.cs | — | 备份恢复 ViewModel |
