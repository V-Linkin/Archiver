# Gatherly Windows (MVP Skeleton)

## 当前状态

本目录是 Windows 版的项目骨架，当前阶段只创建了可编译的 Avalonia 项目结构。

```text
✅ 已完成：
  - Avalonia 项目骨架
  - 最小可运行窗口
  - 项目依赖配置
  - 测试项目骨架

❌ 未完成：
  - 数据库接入（Phase 5C）
  - Repository 实现（Phase 5C）
  - Model 映射（Phase 5C）
  - 首页 / 列表 / 搜索 / 详情（Phase 5D-5F）
  - 回收站（Phase 5F）
  - 导入 / 备份恢复（Phase 5G）
  - Parser / WebView2（Phase 6）
  - 安装包（Phase 7）
```

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
```

需要安装 .NET 8 SDK：

```bash
# macOS
brew install dotnet@8

# Windows
# 下载 https://dotnet.microsoft.com/download/dotnet/8.0
```

## 注意

- 本项目与 macOS 版共享 `shared/` 契约
- 数据库文件 (.db) 跨平台兼容
- 当前阶段未接入数据库，不影响 macOS 构建
