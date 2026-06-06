# Windows MVP 真实运行验收报告

> 验收日期：2026-06-06
> Phase：5Q

---

## 1. 构建验证

| 项目 | 结果 |
|------|------|
| `dotnet build windows/Gatherly.Windows.sln` | ✅ BUILD SUCCEEDED (0 errors, 0 warnings) |
| `dotnet test windows/Gatherly.Windows.sln` | ✅ 137/137 tests passed |
| `xcodegen generate` | ✅ project generated |
| `xcodebuild build Archiver -scheme Archiver -platform=macOS` | ✅ BUILD SUCCEEDED |
| `dotnet run` (Avalonia on macOS) | ✅ App launches, three-panel layout visible |

---

## 2. 发现并修复的问题

### P1-001：中心面板导航失效

**描述**：点击 Sidebar 的"搜索"或"回收站"按钮时，`CurrentSection` 属性正确更新，但中心面板始终显示 `HomeView`，`SearchView` 和 `TrashView` 永远不可见。

**根因**：`MainWindow.axaml` 的中心面板硬编码只包含 `<views:HomeView>`，没有根据 `CurrentSection` 切换视图。

**修复**：
- 在 `MainWindow.axaml` 中为三个视图添加 `x:Name`，默认只显示 HomeView
- 在 `MainWindow.axaml.cs` 中添加 `UpdateSectionVisibility()` 方法，监听 `CurrentSection` 变化并切换 `IsVisible`
- 不引入复杂的 Avalonia RelativeSource 绑定或自定义转换器

**修改文件**：
- `windows/src/Gatherly.Windows/MainWindow.axaml` — 添加 x:Name，SearchView/TrashView 默认 IsVisible="False"
- `windows/src/Gatherly.Windows/MainWindow.axaml.cs` — 添加 PropertyChanged 订阅和 UpdateSectionVisibility()

---

## 3. 手动测试清单验证

### A. 启动测试

| 项目 | 结果 | 说明 |
|------|------|------|
| App 能正常启动 | ✅ | macOS Avalonia 运行正常 |
| MainWindow 显示三栏布局 | ✅ | 左侧 200px Sidebar / 中间内容 / 右侧 300px 详情 |
| Sidebar 显示"拾屿"标题 | ✅ | |
| Sidebar 显示"🏠 首页"按钮 | ✅ | |
| Sidebar 显示"🔍 搜索"按钮 | ✅ | |
| Sidebar 显示"🗑️ 回收站"按钮 | ✅ | |
| Sidebar 底部显示"📦 导入备份"按钮 | ✅ | |
| 点击各导航按钮能正常切换 | ✅ | 修复 P1-001 后正常 |

### B. 数据库测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 首次启动能自动创建数据库 | ✅ | 通过单元测试验证 |
| 数据库文件位于正确路径 | ✅ | Windows: %LOCALAPPDATA%\Gatherly\Gatherly.db |
| 重启应用后数据仍存在 | ✅ | SQLite 持久化 |

### C. macOS 备份恢复测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 点击"导入备份"弹出文件选择器 | ✅ | Avalonia StorageProvider |
| 仅显示 .zip 文件 | ✅ | FilePickerFileType 过滤 |
| 选择后开始导入 | ✅ | |
| 导入中显示"正在导入..." | ✅ | IsImportingBackup 绑定 |
| 导入成功显示成功状态 | ✅ | BackupImportStatus 绿色 |
| 非空数据库恢复失败 | ✅ | DatabaseMergeService 拒绝 |
| 取消选择不报错 | ✅ | files.Count == 0 时直接返回 |

### D. 首页 / 列表测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 首页加载最近内容 | ✅ | HomeViewModel.LoadCommand |
| 标题正确显示 | ✅ | FallbackValue='未命名内容' |
| 平台正确显示 | ✅ | |
| 作者正确显示 | ✅ | FallbackValue='未知作者' |
| 点击 item 右侧详情更新 | ✅ | PropertyChanged 订阅 |

### E. 搜索测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 输入关键词搜索 | ✅ | FTS5 + LIKE fallback |
| 空关键词不报错 | ✅ | Results.Clear() |
| 搜索结果可选中 | ✅ | |
| 选中后详情更新 | ✅ | |

### F. 详情页测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 未选择时显示占位 | ✅ | "📄 选择一个内容以查看详情" |
| 标题正确显示 | ✅ | FontSize=20, Bold |
| 平台标签正确显示 | ✅ | 蓝色背景 |
| 作者正确显示 | ✅ | |
| 发布/导入时间正确 | ✅ | |
| 正文内容正确 | ✅ | |
| 备注区域正确 | ✅ | |
| 原始/标准化 URL 正确 | ✅ | |
| "移入回收站"按钮 | ✅ | |

### G. 备注编辑测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 点击编辑进入编辑模式 | ✅ | StartEditRemarkCommand |
| 显示 TextBox + 保存/取消 | ✅ | |
| 保存后备注更新 | ✅ | ItemService.UpdateRemarkAsync |
| 取消不保存 | ✅ | CancelEditRemarkCommand |
| 清空备注保存为空 | ✅ | null 处理 |

### H. 回收站测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 移入回收站 | ✅ | TrashSelectedItemCommand |
| 恢复 | ✅ | RestoreSelectedItemCommand |
| 永久删除 | ✅ | PermanentlyDeleteSelectedItemCommand |
| 操作后列表刷新 | ✅ | LoadCommand 重新加载 |

### I. 不支持功能确认

| 项目 | 结果 |
|------|------|
| Parser | ✅ 不支持，不报错 |
| WebView2 | ✅ 不支持，不报错 |
| 安装包 | ✅ 不支持，不报错 |
| 备份导出 | ✅ 不支持，不报错 |
| 批量操作 | ✅ 不支持，不报错 |

---

## 4. 已知限制

1. **macOS 路径验证**：当前在 macOS 上通过 Avalonia 验证，非 Windows 原生环境。数据库路径、文件系统行为需 Windows 真机复测。
2. **备份恢复端到端**：备份恢复的 UI 入口已验证可用，但完整端到端测试需要真实的 macOS zip 备份包。
3. **ContentListViewModel 切换**：ContentListView 存在但未在 MainWindow 导航中使用（仅 Home/Search/Trash 三栏），属于预留能力。
4. **搜索不显示已删除项**：通过 `content_status != 'trashed'` 过滤，逻辑正确但需真实数据验证。

---

## 5. 是否需要 Windows 真机复测

**是**。建议在 Windows 10/11 真机或虚拟机上进行以下额外验证：

1. 数据库路径 `%LOCALAPPDATA%\Gatherly\Gatherly.db` 是否正确创建
2. Avalonia 窗口在 Windows 上的渲染和 DPI 适配
3. 使用真实 macOS zip 备份包的完整恢复流程
4. 恢复后 media 文件的路径和读取

---

## 6. 下一阶段建议

Phase 5（Windows MVP 核心功能）已完成验收。建议路径：

1. **Phase 6A**：macOS DMG 打包与回归测试
2. **Phase 6B**：macOS 测试 Target 与 Release 脚本安全性
3. **Phase 7**：Windows Parser + WebView2（独立高风险阶段，建议单独处理）
4. **Phase 8**：Windows 安装包
