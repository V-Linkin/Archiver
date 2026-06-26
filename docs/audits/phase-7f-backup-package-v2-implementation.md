# Phase 7F Backup Package V2 — 实施审计

**日期**: 2026-06-17
**阶段**: Phase 7F-1D-4B-2
**分支**: feature/phase-7f-backup-restore-v2
**HEAD**: a270d7f51af21be7eacf975f3bd6785015e4a0fd（未 commit）

---

## 1. 功能范围

Phase 7F-1 实现 Backup Package V2 **创建**功能：

- VACUUM INTO 数据库快照
- SHA-256 文件完整性
- ZipArchive 压缩
- CollisionResolver 媒体/Logo 碰撞处理
- BackupManifestSchemaValidator (JsonSchema.Net 5.2.0)
- Pending Mapping 阻止规则
- 备份设置页入口
- 备份 UI ViewModel

## 2. 明确不包含

- 备份恢复（Phase 7F-2）
- macOS 代码修改
- 真实用户数据自动迁移

## 3. 冻结目录结构

```
manifest.json
database/archiver.db
media/{itemUUID}/...
platform_logos/{uuid}.png
settings/platform_display_names.json
settings/system_platform_mappings.json
```

## 4. 主要生产代码文件

| 文件 | 说明 |
|---|---|
| `Services/Backup/BackupPackageV2Service.cs` | 核心备份服务 |
| `Services/Backup/BackupPackageV2Manifest.cs` | Manifest 模型 |
| `Services/Backup/BackupManifestSchemaValidator.cs` | Schema 验证器 |
| `Services/Backup/BackupPackageFormatDetector.cs` | 格式检测 |
| `Services/Backup/BackupPathResolver.cs` | 路径安全 |
| `ViewModels/BackupPackageViewModel.cs` | UI ViewModel |
| `ViewModels/BackupPackageServiceAdapter.cs` | 服务适配器 |
| `ViewModels/SettingsViewModel.cs` | 设置页 ViewModel |
| `Views/SettingsView.axaml` | 设置页 UI |

## 5. 主要测试文件

| 文件 | 测试数 |
|---|---|
| `Tests/Services/BackupPackageV2Tests.cs` | 23 |
| `Tests/ViewModels/BackupPackageViewModelTests.cs` | 52 |

## 6. Manifest 与 Schema

- formatVersion: 2
- Schema: `shared/import-export/backup-package-v2.schema.json` (2597 bytes)
- Schema 验证: JsonSchema.Net 5.2.0
- Schema 路径: publish 根目录 `backup-package-v2.schema.json`

## 7. 数据库快照方式

VACUUM INTO 到 staging 目录，然后复制到 ZIP。快照后执行 integrity_check 和 foreign_key_check。

## 8. 文件 Size 与 SHA-256 规则

每个业务文件在 manifest 中记录 path、type、size、sha256、required。SHA-256 为小写 64 字符十六进制。

## 9. packagePath 冲突处理

CollisionResolver 基于文件 SHA-256 去重。相同 hash 复用原路径，不同 hash 追加 `_hash[:12]` 后缀。

## 10. 媒体和 Logo 缺失策略

缺失时抛出 BackupV2Exception，备份失败。Logo 缺失 = 备份失败（冻结规格）。

## 11. Pending Mapping 策略

检查 `system_platform_custom_map.pending.json`。存在未恢复的 pending 时阻止备份。

## 12. 路径安全规则

BackupPathResolver.NormalizePackagePath: 拒绝绝对路径、反斜杠、`..`穿越。所有包内路径使用 `/`。

## 13. 安全覆盖和失败回滚

- 临时文件在 finally 中清理
- 目标文件使用 .bak 备份后替换
- 替换失败时恢复 .bak

## 14. UI 入口与用户流程

- 侧边栏底部"设置"按钮 → SettingsView 独立窗口
- SettingsView 包含"数据备份"区域
- 创建备份 → SaveFilePicker → 进度 → 成功/失败

## 15. 自动测试结果

- 总数: 501
- 通过: 501
- 失败: 0
- 跳过: 0
- 连续 10/10 通过

## 16. build / publish 结果

- dotnet build: 成功，0 错误
- dotnet publish: 成功
- publish 路径: `windows/src/Gatherly.Windows/bin/Release/net8.0/win-x64/publish/`
- 根目录 Schema: 存在，2597 字节
- SHA-256 一致: 是

## 17. 真实数据备份验证结果

- 备份时间: 2026-06-17 22:19 (UTC+8)
- 备份方式: BackupPackageV2Service.CreateBackupAsync (ReadOnly 连接)
- 输出大小: 183,631,367 bytes (175 MB)
- 输出 SHA-256: D2E42BE531335F37401D8838C3616000F13CC312BA120016C7EDAA4E114A7632
- ZIP 条目: 182 (181 业务文件 + manifest.json)
- Schema 验证: PASS
- Size 验证: 181/181 通过
- Hash 验证: 181/181 通过

## 18. ZIP 完整性验证结果

- ZIP 安全: 0 问题（无反斜杠、无穿越、无绝对路径）
- 数据库 integrity_check: ok
- 数据库 foreign_key_check: 0 违规
- user_version: 0（与 manifest 一致）

## 19. 源数据库只读性验证结果

- 备份前 DB: 671744 bytes, SHA256=23CCC0097B286E696A432E367A5F147D584A44C729D47A04FF04CAEE6E602B3B
- 备份后 DB: 671744 bytes, SHA256=23CCC0097B286E696A432E367A5F147D584A44C729D47A04FF04CAEE6E602B3B
- DB Hash 一致: 是
- WAL Hash 一致: 是
- SHM Hash 一致: 是（SHM 时间戳因 SQLite 连接元数据变化，内容未变）

## 20. 人工 UI 验收结果

### 应用启动验证

- 启动方式: Release publish 目录直接执行 Gatherly.Windows.exe
- 可执行文件: `publish/Gatherly.Windows.exe`
- 启动时间: 2026-06-17 23:12:46
- 应用是否正常显示主窗口: 未验证（CLI 环境无法观察窗口）
- 是否出现启动异常: 进程正常启动，无崩溃退出

### 人工 UI 验收明细

#### 用户已人工验证通过

1. **应用启动**: 通过
2. **设置页与备份区域显示**: 通过
3. **创建备份按钮**: 通过
4. **SaveFilePicker 弹出**: 通过
5. **SaveFilePicker 取消**: 通过
6. **默认文件名** (格式: Gatherly-Backup-{yyyyMMdd-HHmmss}.zip): 通过
7. **正常备份启动**: 通过
8. **备份文件成功生成**: 通过
9. **运行中创建按钮禁用**: 通过
10. **运行中取消按钮出现**: 通过
11. **取消按钮可执行**: 通过
12. **阶段名称中文化** (不再显示 HashingFiles): 通过
13. **不再显示真实媒体文件名**: 通过
14. **运行中取消**: 通过
15. **取消后 UI 状态恢复**: 通过
16. **取消后没有遗留损坏 ZIP**: 通过
17. **取消后重新创建备份**: 通过
18. **覆盖拒绝**: 通过
19. **覆盖允许**: 通过

#### 用户延期验证

20. **错误状态与失败后重试**: 未验证 — 用户明确决定延期验证
    - 底层自动测试结果: 通过（新增 20 个错误路径回归测试）

#### 未执行

21. **关闭和重新启动** (人工观察窗口): 未验证 — 应用进程启动正常、Responding=True，但 CLI 环境无法观察窗口关闭/重启的 GUI 行为

## 21. 已知限制和未实现项

- 人工 UI 验收未执行
- 恢复功能未实现
- 备份进度条 UI 在快速操作中可能闪烁

## 22. commit / push 状态

- commit: 否
- push: 否

## 23. 本轮发现并修复的生产 Bug

**CollectMediaFilesAsync 和 CollectLogoFilesAsync 路径拼接 Bug**:

- 原因: `mediaDir` 参数已包含 `media/` 前缀，但 `finalPkgPath` 也以 `media/` 开头，Path.Combine 产生 `media/media/` 双层目录
- 影响: 所有包含媒体文件的真实备份均会在验证阶段失败
- 修复: 使用 `finalPkgPath["media/".Length..]` 去除前缀后与 mediaDir 组合
- 同类问题在 CollectLogoFilesAsync 中一并修复
