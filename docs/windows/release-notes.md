# 拾屿 Gatherly Windows Release Notes

> 当前稳定分支：recover/phase-7d3-youtube
> 当前稳定提交：ac48159
> 测试基线：331/331
> 更新日期：2026-06-12

---

## 概述

拾屿 Gatherly Windows 是 macOS 版的跨平台移植版本，基于 Avalonia UI + C# + .NET 8 构建。支持从 macOS 备份 zip 导入数据，并在 Windows 上浏览、搜索、管理归档内容。

---

## Phase 7D-3：YouTube Parser + 平台合并 + 详情页交互

### YouTube Parser（Phase 7D-3）

- 支持 watch / youtu.be / shorts 链接
- 使用 HTML meta + ytInitialPlayerResponse JSON 解析
- 可读取标题 / 频道 / 简介 / 封面 / 播放量 / 时长
- 下载封面到本地 media
- 不下载 YouTube 视频，不使用 API Key，不使用 WebView2
- platform = youtube, custom_platform_id = null

### Fix-A：Duplicate/import_tasks 规则修复

- completed/success + item_id NULL 允许重导（orphan task）
- orphan item_id 允许重导
- failed / parser_not_implemented 等失败任务允许重导
- 回收站 item 返回 DuplicateInTrash
- 彻底删除后允许再次导入

### Fix-A.1：stale pending/importing 修复

- import_tasks 新增 updated_at 列（v3 migration）
- UTC Unix timestamp seconds
- 10 分钟 active window
- <=10 分钟 pending/importing 阻止重导
- >10 分钟视为 stale，允许重试
- 旧数据 updated_at 回填 created_at

### Fix-B：平台显示与合并修复

- YouTube 一个入口显示标准 + macOS 备份 custom 内容
- B站一个入口显示标准 + macOS 备份 custom 内容
- 小红书保持 custom 查询
- merged 白名单当前仅 YouTube、B站
- SQLite GUID 文本大小写敏感问题修复（COLLATE NOCASE）
- count、Repository、ViewModel、UI 数量保持一致

### Fix-C：详情页交互修复

- 正文由 TextBlock 改为 SelectableTextBlock
- 支持鼠标选择、Ctrl+C、右键复制、全选
- 正文 URL 解析（http/https）
- 正文链接在正文下方独立区域展示
- 原始链接可点击、可复制
- 仅允许 http/https，使用系统默认浏览器
- javascript/file/data/ftp 被拒绝

---

## 支持能力

| 功能 | 状态 |
|------|------|
| 粘贴链接识别平台（URL Normalizer） | ✅ |
| 粘贴链接创建导入任务 | ✅ |
| GitHub 链接自动导入 | ✅ |
| B站链接自动导入 | ✅ |
| YouTube 链接自动导入 | ✅ |
| macOS 备份 zip 导入 | ✅ |
| 首页卡片浏览 | ✅ |
| 平台分类浏览 | ✅ |
| 未分类内容浏览 | ✅ |
| 搜索（卡片结果） | ✅ |
| 搜索 Enter 快捷键 | ✅ |
| 完整详情页 | ✅ |
| 详情页正文可选择/复制 | ✅ |
| 详情页链接可点击 | ✅ |
| 图片横向滚动展示 | ✅ |
| 图片完整显示（不裁切） | ✅ |
| 图片查看器（上一张/下一张） | ✅ |
| 视频文件打开 | ✅ |
| 视频所在文件夹打开 | ✅ |
| 自定义平台名称显示 | ✅ |
| 回收站（移入/恢复/彻底删除） | ✅ |
| 剩余天数显示 | ✅ |
| 备注编辑 | ✅ |
| 导入备份文件选择器 | ✅ |
| stale import task 自动重试 | ✅ |

---

## 不支持能力

| 功能 | 说明 |
|------|------|
| 平台管理 UI | 不能新建/重命名/删除/排序自定义平台 |
| 其它平台 Parser | 仅支持 GitHub / B站 / YouTube，其它平台后续实现 |
| WebView2 浏览器嵌入 | 依赖 Windows WebView2 SDK |
| Windows 安装包 (MSI/EXE) | 需打包工具，后续阶段实现 |
| 云同步 | 纯本地应用，不支持云同步 |
| 视频内嵌播放器 | 使用系统默认播放器打开 |
| Markdown 渲染 | 当前为纯文本显示 |
| 备份导出 | 仅支持从 macOS 导入，不支持导出 |
| YouTube 视频下载 | 当前仅解析元数据，不下载视频文件 |
| 正文行内链接 | 链接在正文下方独立区域显示 |

---

## 技术栈

```text
UI:       Avalonia 11.1
Language: C# 12
Runtime:  .NET 8 (self-contained)
Database: SQLite (Microsoft.Data.Sqlite 8.0)
MVVM:     CommunityToolkit.Mvvm 8.2
```

---

## 系统要求

```text
OS:       Windows 10 或更高版本
Arch:     x64
Runtime:  无需安装 .NET（self-contained 发布）
```

---

## 已知问题

详见 [known-limitations.md](known-limitations.md)
