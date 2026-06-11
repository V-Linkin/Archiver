# 拾屿 Gatherly Windows MVP Release Notes

> 版本：Windows MVP RC
> 基准 commit：1d4899f
> 发布日期：2026-06-11

---

## 概述

拾屿 Gatherly Windows MVP 是 macOS 版的跨平台移植版本，基于 Avalonia UI + C# + .NET 8 构建。支持从 macOS 备份 zip 导入数据，并在 Windows 上浏览、搜索、管理归档内容。

---

## 支持能力

| 功能 | 状态 |
|------|------|
| 粘贴链接识别平台（URL Normalizer） | ✅ |
| 粘贴链接创建导入任务 | ✅ |
| GitHub 链接自动导入 | ✅ |
| macOS 备份 zip 导入 | ✅ |
| 首页卡片浏览 | ✅ |
| 平台分类浏览 | ✅ |
| 未分类内容浏览 | ✅ |
| 搜索（卡片结果） | ✅ |
| 搜索 Enter 快捷键 | ✅ |
| 完整详情页 | ✅ |
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

---

## 不支持能力

| 功能 | 说明 |
|------|------|
| 其它平台自动抓取内容 | 仅支持 GitHub，其它平台 Parser 后续阶段实现 |
| Parser 内容抓取 | 需逐平台实现，高风险阶段 |
| WebView2 浏览器嵌入 | 依赖 Windows WebView2 SDK |
| Windows 安装包 (MSI/EXE) | 需打包工具，后续阶段实现 |
| 云同步 | 纯本地应用，不支持云同步 |
| 视频内嵌播放器 | 使用系统默认播放器打开 |
| Markdown 渲染 | 当前为纯文本显示 |
| 备份导出 | 仅支持从 macOS 导入，不支持导出 |

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

---

## 用户指南

详见 [windows-user-guide.md](windows-user-guide.md)
