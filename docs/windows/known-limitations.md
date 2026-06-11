# 拾屿 Gatherly Windows MVP 已知限制

> 基准：commit 1d4899f
> 更新日期：2026-06-11

---

## 功能限制

### 1. 粘贴链接仅支持 GitHub 自动导入

Windows 版支持粘贴 GitHub 链接并自动抓取内容导入（Phase 7D-1）。其它平台（B站、YouTube、小红书等）支持创建导入任务但暂不抓取内容。

**原因：** 其它平台 Parser 将在后续 Phase 7D/7E 逐步实现。

### 2. 不支持内容抓取（Parser）

Windows 版没有实现网页内容抓取功能。无法自动从 URL 获取标题、正文、图片等信息。

**原因：** 每个平台网页结构不同，需要逐个适配。

### 3. 不支持 WebView2 浏览器嵌入

Windows 版没有嵌入浏览器组件。

**原因：** 依赖 Windows WebView2 SDK，集成复杂度高。

### 4. 需要从 macOS 备份导入数据

Windows 版目前只能通过导入 macOS 备份 zip 来获取数据。无法在 Windows 上直接创建或导入内容。

### 5. 视频使用系统默认播放器

视频文件使用系统默认播放器打开，不支持内嵌播放。

---

## 技术限制

### 6. Self-contained 发布包

当前是 self-contained publish 包，不是正式安装器（MSI/EXE）。用户需要手动解压运行。

### 7. 数据目录固定

数据目录固定为 `%LOCALAPPDATA%\Gatherly\`，暂不支持自定义。

### 8. 不支持备份导出

目前只支持从 macOS 导入备份，不支持从 Windows 导出备份。

---

## UI 限制

### 9. 部分 UI 仍属于 P2

以下 UI 细节属于 P2，不影响核心功能：

- 部分间距和排版细节
- 部分文案优化
- 部分视觉一致性

---

## 后续计划

| 功能 | 计划阶段 |
|------|----------|
| Parser + WebView2 | Phase 7 |
| Windows 安装包 | Phase 8 |
| 备份导出 | 后续阶段 |
| Markdown 渲染 | 后续阶段 |
| 视频内嵌播放 | 后续阶段 |
