# Windows MVP 范围定义

## 1. 概述

Windows MVP 的目标是验证跨平台数据兼容性和核心功能可用性，不追求一次性与 macOS 功能完全对齐。

核心原则：

```text
1. 数据库兼容：直接复用 macOS 的 .db 文件
2. 核心列表：首页、平台、文件夹、自定义平台、未分类
3. 搜索：FTS5 全文搜索
4. 内容详情：显示 item 信息和媒体
5. 回收站：移入、恢复、永久删除
6. 基础导入：支持当前备份格式恢复
```

## 2. MVP 必须包含

| 功能 | 优先级 | 说明 |
|------|--------|------|
| 打开/创建本地 SQLite 数据库 | P0 | 直接复用 macOS .db 文件 |
| 读取 shared/db migrations | P0 | v1_createTables, v2_fts |
| 显示首页最近内容 | P0 | 最近 7 天导入 |
| 显示平台分类 | P0 | 10 个内置平台 + 自定义 |
| 显示文件夹内容 | P0 | 支持嵌套文件夹 |
| 显示自定义平台内容 | P0 | 按 customPlatformID 筛选 |
| 显示未分类内容 | P0 | platform=.custom 且 customPlatformID IS NULL |
| 搜索 | P0 | FTS5 全文搜索，复用 macOS 的 items_fts |
| 内容详情 | P0 | 显示 title, body, author, 平台, 日期等 |
| 移入回收站 | P0 | 设置 deletedAt + contentStatus=trashed + TrashRecord |
| 恢复 | P0 | 清除 deletedAt |
| 永久删除 | P0 | 删除 item + TrashRecord + 媒体文件 |
| 基础设置页 | P1 | 数据目录选择、关于信息 |
| 导入备份恢复 | P1 | 支持当前 macOS 备份格式 |

## 3. MVP 暂不包含

| 功能 | 原因 | 建议阶段 |
|------|------|---------|
| 完整 Parser 抓取 | 需要逐平台实现 HTTP/WebView2 | Phase 6+ |
| WebView2 解析 | 依赖 Windows 平台 WebView2 SDK | Phase 6+ |
| 自动更新 | 需要安装包和更新服务 | Phase 7+ |
| 高级媒体导出 | NSOpenPanel/NSSavePanel 无 Windows 等价 | Phase 6+ |
| 复杂 Markdown 编辑器 | 非核心功能 | Phase 7+ |
| macOS 专属快捷操作 | 菜单栏、Finder 集成 | 不迁移 |
| 系统托盘 | Windows 专属实现 | Phase 6+ |
| 全局快捷键 | Windows 专属实现 | Phase 7+ |
| 文件关联 | Windows 注册表操作 | Phase 7+ |
| DPI 缩放适配 | 需要 Windows 真机测试 | Phase 6+ |

## 4. 数据兼容性要求

```text
1. Windows 端打开 macOS 创建的 .db 文件，数据完整显示
2. Windows 端创建的 .db 文件，macOS 端可以打开
3. FTS5 索引兼容
4. 枚举 rawValue 完全一致
5. normalizedURL / platformContentID 规则完全一致
6. 时间字段使用 Unix timestamp seconds
7. UUID 使用 TEXT 存储
```

## 5. UI 适配要求

```text
1. 使用 Avalonia 原生控件，不模仿 macOS SwiftUI 样式
2. 侧边栏导航：首页、平台列表、文件夹、搜索、回收站、设置
3. 内容列表：网格/列表双视图
4. 内容详情：标题、正文、作者、平台、日期、媒体
5. 深色/浅色主题跟随系统
6. 中文界面
```

## 6. 验收标准

```text
1. Windows 端可以打开 macOS 的 .db 文件，所有数据显示正确
2. 搜索结果与 macOS 一致
3. 回收站操作与 macOS 一致
4. 自定义平台创建/删除/移动与 macOS 一致
5. 文件夹创建/重命名/删除与 macOS 一致
6. 基础备份恢复可以导入 macOS 导出的备份
```
