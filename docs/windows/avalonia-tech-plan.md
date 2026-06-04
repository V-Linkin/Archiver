# Avalonia 技术方案

## 1. 技术栈

```text
UI 框架:     Avalonia 11.x
语言:        C# 12+
运行时:      .NET 8+
数据库:      SQLite
SQLite 库:   Microsoft.Data.Sqlite 或 SQLitePCLRaw
架构模式:    MVVM (CommunityToolkit.Mvvm)
DI 框架:     Microsoft.Extensions.DependencyInjection
异步:        async/await + CommunityToolkit.Mvvm ObservableObject
CI:          GitHub Actions (windows-latest)
```

## 2. macOS 可完成的开发

以下工作可以在 macOS 上完成，不需要 Windows 环境：

```text
C# 业务代码编写和单元测试
SQLite Repository 实现和测试
shared/db migration 读取和执行
shared/model JSON Schema 到 C# Model 映射
URL normalizer 规则文档和测试用例（JSON）
parser fixtures schema 和测试数据
基础 Avalonia UI 布局（.axaml 文件）
ViewModel 逻辑
Service 层逻辑
数据目录管理逻辑
导入/备份格式解析逻辑
```

## 3. 必须在 Windows 上测试

以下功能依赖 Windows 平台能力，必须在 Windows 环境验证：

```text
WebView2 集成和 JavaScript 交互
安装包构建和安装流程
Windows 文件路径处理（C:\Users\...）
系统托盘和通知
全局快捷键注册
DPI 缩放和多显示器
文件关联（.db 文件双击打开）
自动更新流程
Windows 特定的文件系统权限
```

## 4. SQLite 兼容方案

```text
1. 使用 Microsoft.Data.Sqlite 读写 .db 文件
2. 直接复用 shared/db/migrations/ 下的 SQL 文件
3. 通过 EmbeddedResource 或文件路径加载 SQL
4. FTS5 需要确认 SQLite 版本支持（Microsoft.Data.Sqlite 默认包含）
5. 事务和 WAL 模式与 macOS GRDB 兼容
```

## 5. 数据目录方案

```text
Windows 默认数据目录:
  %LOCALAPPDATA%\Gatherly\

结构:
  %LOCALAPPDATA%\Gatherly\
    Gatherly.db
    media/
      images/
      videos/
      thumbnails/
    platform_logos/

macOS 数据目录（参考）:
  ~/Library/Application Support/Gatherly/
```

## 6. MVVM 架构

```text
View (.axaml)  →  ViewModel  →  Service  →  Repository  →  SQLite

Model:          C# POCO，对应 shared/model/ JSON Schema
ViewModel:      ObservableObject，管理 UI 状态
Service:        业务逻辑，对应 macOS 的 ItemService / FolderService / SearchService
Repository:     数据访问，对应 macOS 的 ItemRepository / FolderRepository 等
```

## 7. 共享契约引用

```text
Windows 端不直接复用 Swift 代码，而是按 shared/ 契约实现 C# 版本:

shared/db/migrations/      → 读取 SQL 文件执行 migration
shared/model/enums.json    → 生成 C# enum
shared/model/*.schema.json → 生成 C# Model
shared/url/                → 实现 C# URLNormalizer
shared/parsers/fixtures/   → 用于 Parser 单元测试
shared/import-export/      → 实现 C# 导入/导出
```

## 8. 开发阶段建议

```text
Phase 5B: 创建 Avalonia 项目骨架 + 数据库层
Phase 5C: 实现 Repository 层（C# 版本）
Phase 5D: 实现首页 + 平台列表 + 文件夹
Phase 5E: 实现搜索 + 内容详情
Phase 5F: 实现回收站 + 恢复/永久删除
Phase 5G: 实现基础导入/备份恢复
Phase 5H: Windows 真机测试 + 修复
Phase 6:  Parser + WebView2
Phase 7:  安装包 + 自动更新
```

## 9. CI 方案

```text
GitHub Actions workflow:
  - trigger: push to main / PR
  - runner: windows-latest
  - steps:
    1. checkout
    2. setup .NET 8
    3. dotnet restore
    4. dotnet build
    5. dotnet test
    6. dotnet publish (可选)
```
