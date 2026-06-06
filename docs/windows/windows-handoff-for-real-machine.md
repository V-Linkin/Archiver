# Windows 真机接手指南

> 本文档用于在真实 Windows 电脑上接手并验证 Gatherly Windows MVP。
> 最后更新：Phase 6C-Prep

---

## 1. 当前项目状态

Windows MVP 核心功能闭环已完成（Phase 5A–5Q），可在 macOS 上 build / test / run，但尚未在 Windows 真机上验证运行。

### 已完成能力

```text
✅ Avalonia Windows 项目骨架
✅ SQLite 数据库初始化 + shared/db migration 执行
✅ C# Models / Enums（与 macOS 完全一致）
✅ Repository 读写（ItemRepository, FolderRepository, TrashRepository, SearchRepository 等）
✅ Service 层（HomeDataService, SearchService, ItemService, BackupImportService 等）
✅ ViewModel 层（HomeViewModel, SearchViewModel, TrashViewModel, MainWindowViewModel）
✅ 三栏 UI（Sidebar / 中间内容 / 右侧详情）
✅ 首页最近内容
✅ 搜索（FTS5 + LIKE fallback）
✅ 内容列表
✅ 内容详情只读展示
✅ 备注编辑
✅ 移入回收站
✅ 回收站恢复
✅ 永久删除
✅ 回收站 UI 按钮（恢复 / 永久删除）
✅ macOS zip 备份恢复 Service
✅ macOS zip 备份恢复 UI 入口
✅ Windows MVP 验收文档 + 手动测试清单
✅ Phase 5Q 已修复 Sidebar 中心面板切换问题（搜索/回收站页面可正常切换显示）
```

### 测试覆盖

```text
dotnet test: 137/137 全部通过
```

---

## 2. Windows 电脑第一步

### 2.1 确认 .NET 8 SDK

```bash
dotnet --version
# 应输出 8.0.x

dotnet --list-sdks
# 应至少包含 8.0.x
```

如果未安装，从 https://dotnet.microsoft.com/download/dotnet/8.0 下载 Windows x64 安装包。

### 2.2 拉取代码

```bash
git pull
```

### 2.3 构建

```bash
dotnet build windows/Gatherly.Windows.sln
```

预期输出：

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### 2.4 运行测试

```bash
dotnet test windows/Gatherly.Windows.sln
```

预期输出：

```text
已通过! - 失败: 0, 通过: 137, 已跳过: 0, 总计: 137
```

### 2.5 启动 App

```bash
dotnet run --project windows/src/Gatherly.Windows/Gatherly.Windows.csproj
```

应弹出 Avalonia 窗口，显示三栏布局：
- 左侧 Sidebar：拾屿标题 + 首页/搜索/回收站/导入备份
- 中间内容区
- 右侧详情区

---

## 3. 生成 .exe（可选）

### 3.1 发布为独立 exe

```bash
dotnet publish windows/src/Gatherly.Windows/Gatherly.Windows.csproj -c Release -r win-x64 --self-contained true
```

### 3.2 输出目录

```text
windows/src/Gatherly.Windows/bin/Release/net8.0/win-x64/publish/
```

应包含：

```text
Gatherly.Windows.exe
Gatherly.Windows.dll
Gatherly.Windows.deps.json
...其他依赖
```

### 3.3 运行 exe

双击 `Gatherly.Windows.exe` 或在命令行中：

```bash
.\Gatherly.Windows.exe
```

---

## 4. Windows 数据路径

数据库和媒体文件存储在：

```text
%LOCALAPPDATA%\Gatherly\Gatherly.db          # SQLite 数据库
%LOCALAPPDATA%\Gatherly\media\               # 媒体文件（备份恢复时复制）
%LOCALAPPDATA%\Gatherly\platform_logos\       # 平台 Logo（备份恢复时复制）
```

在文件资源管理器中可直接输入 `%LOCALAPPDATA%\Gatherly` 访问。

---

## 5. macOS zip 备份恢复测试

### 5.1 准备测试数据

1. 在 macOS 上打开"拾屿 Archiver"
2. 确保有若干内容（标题、正文、作者、备注等）
3. 点击菜单栏 → 导出备份 → 保存为 `.zip` 文件
4. 将 zip 文件传到 Windows 电脑（U盘/网络/云盘）

### 5.2 在 Windows 上恢复

1. 启动 Gatherly Windows
2. 确保当前数据库为空（首次启动即可）
3. 点击 Sidebar 底部"📦 导入备份"
4. 在文件选择器中选择 macOS 导出的 `.zip` 文件
5. 等待导入完成

### 5.3 验证

```text
✅ 导入成功后显示"导入成功"（绿色文字）
✅ 首页出现恢复的内容
✅ 搜索能搜到恢复的内容
✅ 详情页能显示标题/平台/作者/正文/备注
✅ 回收站中有已删除的内容（如有）
```

### 5.4 重要限制

- **当前只支持恢复到空数据库**：如果已有数据，恢复会失败并显示错误
- 如果需要重新恢复，需手动删除 `%LOCALAPPDATA%\Gatherly\Gatherly.db` 后重启

---

## 6. 当前不要做的事情

```text
❌ 不要直接开始 Parser
❌ 不要直接开始 WebView2
❌ 不要直接做安装包（.msi / .exe installer）
❌ 不要改数据库 schema
❌ 不要改 shared/ 契约
❌ 不要重构 Windows 架构
❌ 不要继续堆新功能
❌ 不要改 macOS 业务代码
❌ 不要做备份导出
❌ 不要做 manifest.json 新格式
❌ 不要做自动更新
❌ 不要修改 shared/db/ 下的 SQL 文件
❌ 不要修改 shared/model/ 下的 JSON Schema
```

这些功能有独立的后续 Phase 规划，需要在 macOS 端同步设计后再执行。

---

## 7. 问题分级

发现 bug 时请按以下级别分类：

### P0 阻塞

```text
App 无法启动
数据库无法创建
备份恢复完全不可用
核心页面崩溃（未选择 item 时、空列表时等）
```

### P1 高优先级

```text
核心功能可启动但无法正常使用
搜索无结果（但数据库有数据）
详情不更新（但 item 已选中）
回收站命令失败
导航切换不工作
```

### P2 中优先级

```text
UI 显示不美观
状态文字不准确
交互不顺畅
DPI 适配问题
字体/颜色不协调
```

### P3 低优先级

```text
文案优化
样式微调
细节改进
```

---

## 8. 验证通过后的下一步

Windows 真机验证通过后，建议按以下顺序推进：

1. **文档更新**：将验证结果写入 `docs/windows/windows-mvp-test-report.md`
2. **macOS 回归**：确认 Windows 改动不影响 macOS 构建
3. **Phase 6A**：macOS DMG 打包与回归测试
4. **Phase 6B**：macOS 测试 Target 与 Release 脚本安全性
5. **Phase 7**：Windows Parser + WebView2（独立高风险阶段）
6. **Phase 8**：Windows 安装包

---

## 9. 参考文档

| 文档 | 路径 | 说明 |
|------|------|------|
| MVP 范围定义 | `docs/windows/windows-mvp-scope.md` | MVP 功能边界 |
| Avalonia 技术方案 | `docs/windows/avalonia-tech-plan.md` | 技术栈和平台约束 |
| 项目结构规划 | `docs/windows/windows-project-structure.md` | 目录和文件规划 |
| MVP 验收清单 | `docs/windows/windows-mvp-acceptance.md` | 当前完成能力 |
| 手动测试清单 | `docs/windows/windows-manual-test-checklist.md` | 逐项勾选测试 |
| 真实运行验收报告 | `docs/windows/windows-mvp-test-report.md` | Phase 5Q 验收结果 |
| 跨平台进度 | `docs/architecture/cross-platform-progress.md` | 全局进度和规范 |
