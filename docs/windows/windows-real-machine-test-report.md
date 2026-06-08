# Windows 真机 / exe 运行验证报告

> 验证日期：2026-06-07
> Phase：6C

---

## 1. 环境信息

| 项目 | 值 |
|------|-----|
| OS | Microsoft Windows 10 家庭版 |
| OS Build | 19045 |
| .NET SDK | 8.0.421 |
| .NET Runtime | Microsoft.NETCore.App 8.0.27 |
| Architecture | x64 |

---

## 2. 构建与测试验证

| 项目 | 结果 | 说明 |
|------|------|------|
| `dotnet build windows/Gatherly.Windows.sln` | ✅ | 0 errors, 0 warnings |
| `dotnet test windows/Gatherly.Windows.sln` | ✅ | 137/137 passed |

---

## 3. 运行时验证

### 3.1 `dotnet run` 验证

| 项目 | 结果 | 说明 |
|------|------|------|
| App 能否启动 | ✅ | dotnet run 后进程正常启动 |
| MainWindow 三栏布局 | ⚠️ | 本次为 headless 启动验证，未做 GUI 逐项截屏 |
| Sidebar 节点 | ⚠️ | 未做 GUI 逐项点击 |
| 导航切换 | ⚠️ | 未做 GUI 逐项点击 |
| 运行时异常 | ✅ | 未发现启动崩溃或未捕获异常 |
| Binding warning | ⚠️ | 未做控制台 trace 逐条审计 |
| 窗口缩放 | ⚠️ | 未做人工截图对比 |
| 字体显示 | ⚠️ | 未做人工截图对比 |
| 文件选择器 | ⚠️ | 未做 GUI 点击验证 |

### 3.2 `dotnet publish` 验证

| 项目 | 结果 | 说明 |
|------|------|------|
| `dotnet publish ... -c Release -r win-x64 --self-contained true` | ✅ | publish 成功 |
| 输出目录 | ✅ | `windows\src\Gatherly.Windows\bin\Release\net8.0\win-x64\publish\` |
| Gatherly.Windows.exe 存在 | ✅ | exe / dll / pdb 均已生成 |

### 3.3 exe 双击 / 进程启动验证

| 项目 | 结果 | 说明 |
|------|------|------|
| Gatherly.Windows.exe 是否可启动 | ✅ | 可通过 Start-Process 正常拉起并短暂运行 |
| 是否能创建数据库 | ✅ | 启动后创建 `%LOCALAPPDATA%\Gatherly\Gatherly.db` |
| 是否能打开窗口 | ⚠️ | 未做人工 GUI 确认 |
| 首页 / 搜索 / 回收站切换 | ⚠️ | 未做人工 GUI 确认 |
| 关闭后重新打开是否正常 | ✅ | 再次启动后本地数据文件仍存在 |

---

## 4. Windows 数据路径验证

| 路径 | 是否存在 | 说明 |
|------|------|------|
| `%LOCALAPPDATA%\Gatherly\` | ✅ | 已自动创建 |
| `%LOCALAPPDATA%\Gatherly\Gatherly.db` | ✅ | 数据库文件已创建 |
| `%LOCALAPPDATA%\Gatherly\media\` | ⏳ | 当前空库启动未生成，属预期 |
| `%LOCALAPPDATA%\Gatherly\platform_logos\` | ⏳ | 当前空库启动未生成，属预期 |

---

## 5. macOS zip 备份恢复验证

| 项目 | 结果 | 说明 |
|------|------|------|
| 导入备份 UI 入口 | ✅ | 代码入口存在 |
| 文件选择器限制 `.zip` | ✅ | FilePickerFileType 已限制 |
| 非空数据库恢复拒绝 | ✅ | DatabaseMergeService 逻辑存在 |
| 本次端到端恢复验证 | ⏳ | 未在此轮执行真实 zip 导入 |

---

## 6. 手动测试清单验证结果

### A. 启动测试

| 项目 | 结果 | 说明 |
|------|------|------|
| App 能正常启动，无崩溃 | ✅ | dotnet run / exe 均可启动 |
| MainWindow 显示三栏布局 | ⚠️ | 需 GUI 手动确认 |
| Sidebar 显示“拾荒”标题 | ⚠️ | 需 GUI 手动确认 |
| Sidebar 显示“🍎 首页”按钮 | ⚠️ | 需 GUI 手动确认 |
| Sidebar 显示“🔍 搜索”按钮 | ⚠️ | 需 GUI 手动确认 |
| Sidebar 显示“🗑 回收站”按钮 | ⚠️ | 需 GUI 手动确认 |
| Sidebar 底部显示“📥 导入备份”按钮 | ⚠️ | 需 GUI 手动确认 |
| 点击各导航按钮能正常切换 | ⚠️ | 需 GUI 手动确认 |

### B. 数据库测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 首次启动能自动创建数据库 | ✅ | 已确认 |
| 数据库文件位于 `%LOCALAPPDATA%\Gatherly\Gatherly.db` | ✅ | 已确认 |
| 数据目录位于 `%LOCALAPPDATA%\Gatherly\` | ✅ | 已确认 |
| 重启应用后数据仍存在 | ✅ | SQLite 持久化已验证 |

### C. macOS 备份恢复测试

| 项目 | 结果 | 说明 |
|------|------|------|
| C1 正常恢复 | ⏳ | 未执行真实 zip |
| C2 异常处理 | ⏳ | 未执行真实 zip |

### D. 首页 / 列表测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 列表加载、标题、平台、作者、时间、选中切换 | ⚠️ | 需 GUI 手动确认 |

### E. 搜索测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 英文/中文/空关键词、可选中、详情更新、已删除内容不出现 | ⚠️ | 需 GUI 手动确认 |

### F. 详情页测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 未选中占位、已选中字段、移入回收站按钮 | ⚠️ | 需 GUI 手动确认 |

### G. 备注编辑测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 编辑、保存、取消、清空、重启持久化 | ⚠️ | 需 GUI 手动确认 |

### H. 回收站测试

| 项目 | 结果 | 说明 |
|------|------|------|
| 移入回收站、恢复、永久删除 | ⚠️ | 需 GUI 手动确认 |

### I. 不支持功能确认

| 项目 | 结果 | 说明 |
|------|------|------|
| 不测试 Parser / WebView2 / 安装包 / 自动更新 / 备份导出 / manifest / 批量 / 新建内容 / 标题正文编辑 / 移动文件夹 | ✅ | 符合当前阶段要求 |

---

## 7. 问题分级

### P0

无。

### P1

无。

### P2

1. 当前报告中部分 GUI 交互项为 `⚠️`，尚未完成人工 GUI 点击验证。

### P3

1. 当前报告为 Windows headless 启动级验证，非完整 GUI 人工走查。

---

## 8. 代码变更

| 项目 | 结果 |
|------|------|
| 是否修改代码 | 否 |
| 修改文件 | 无 |

---

## 9. 结论

- Windows build/test 已通过。
- Windows `dotnet run` 与 `publish` exe 可正常启动并创建本地数据路径。
- 当前未发现 P0 / P1 阻塞问题。
- 剩余待补项主要是 GUI 人工逐项确认，属于 P2 项，不影响“Windows MVP 已能在真实 Windows 机器上运行”的判断。

### 是否可以进入下一阶段

可以继续推进，但建议下一轮补做一次完整 GUI 走查以消除 `⚠️` 项。
