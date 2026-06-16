# Phase 7E 系统来源 → CustomPlatform 迁移审计

> 分支: feature/phase-7e-platform-management
> HEAD: a28df1d
> 审计时间: 2026-06-16

---

## 一、历史首次迁移前备份

**结论：缺失，无法追溯补回。**

首次迁移发生在前几轮真机测试中，迁移服务在应用启动时自动执行。当时未实现备份机制，因此首次迁移前没有创建任何数据库快照。

本轮已明确标识此缺失。

---

## 二、当前状态安全备份

**备份路径**: `%LOCALAPPDATA%\Gatherly\backups\post-system-platform-migration-current-state_20260616_165902`

**包含内容**:
- `Gatherly.db` (671,744 bytes)
- `Gatherly.db-wal` / `Gatherly.db-shm`
- `media/` (2,110 文件)
- `platform_logos/`
- `platform_display_names.json`
- `system_platform_custom_map.json`
- `README.md`

---

## 三、未来迁移前强制备份门禁（已实现）

**代码位置**: 
- `Services/MigrationBackupService.cs` — 备份创建
- `Services/SystemPlatformItemMigrationService.cs` line 40-58 — 门禁逻辑

**执行顺序**:
1. `BuildMigrationPlan()` 扫描 orphan items
2. 如果 `TotalSourceItemCount == 0` → 返回 `NoWork()`，不创建备份
3. 调用 `MigrationBackupService.CreatePreMigrationBackupAsync()`
4. 如果 `backup.Success == false` → 返回 `BlockedByBackupFailure()`，不进入数据库事务
5. 只有备份成功才执行 `ExecuteMigration()`

**备份内容**:
- `VACUUM INTO` 创建可独立打开的数据库快照
- `media/` 目录
- `platform_logos/` 目录
- `platform_display_names.json`
- `system_platform_custom_map.json`
- `manifest.json`（含 backupType, createdAt, migrationPlan 等）

**测试覆盖** (8个测试):
- NoWork_SkipsBackupAndMigration
- BackupSuccess_MigrationProceeds
- BackupFailure_BlocksMigration
- BackupFailure_NoCustomPlatformCreated
- BackupFailure_MappingNotWritten
- Plan_HasCorrectFields
- Plan_ZeroOrphans_ReturnsEmpty
- Backup_VerifiesSnapshotReadable

---

## 四、迁移映射

**映射文件**: `%LOCALAPPDATA%\Gatherly\system_platform_custom_map.json`

```json
{
  "version": 1,
  "mappings": {
    "bilibili": "c9bb75ca-1c10-4520-88b1-421e14e5024f",
    "github": "7f912519-8e4b-417d-8489-4f6e9b6f4594",
    "youtube": "8b8b33d0-7696-4ba3-ac54-c93c99952428"
  }
}
```

| System Raw Value | Target CustomPlatform | Name | Items Migrated |
|---|---|---|---|
| bilibili | c9bb75ca-... | 哔哩哔哩 | 1 |
| github | 7f912519-... | Github | 1 |
| youtube | 8b8b33d0-... | Youtube | 4 |

**映射基于 raw value → UUID**，不依赖显示名称。

---

## 五、迁移后验证

### 系统来源 orphan 检查

```sql
SELECT lower(platform), COUNT(*) FROM items
WHERE deleted_at IS NULL AND custom_platform_id IS NULL
AND lower(platform) IN ('douyin','xiaohongshu','coolapk','bilibili','github','youtube','x','weibo','zhihu','douban')
GROUP BY lower(platform);
```

**结果：无数据** — 所有系统来源 item 已迁移。

### 孤立 custom_platform_id 检查

```sql
SELECT i.id FROM items i
LEFT JOIN custom_platforms cp ON cp.id = i.custom_platform_id COLLATE NOCASE
WHERE i.custom_platform_id IS NOT NULL AND cp.id IS NULL;
```

**结果：无数据** — 所有 custom_platform_id 指向有效 CustomPlatform。

### 外键检查

```
PRAGMA foreign_keys = 1
PRAGMA foreign_key_check = 无结果
PRAGMA integrity_check = ok
```

### 数据总量

| 表 | 数量 |
|---|---|
| items | 67 |
| media_assets | 179 |
| custom_platforms | 9 |
| import_tasks | 41 |
| trash_records | 28 |

---

## 六、未分类 B站内容分析

### 未分类 B站 item

**ID**: `93E92C29-167C-4AAC-91CC-B62FE5B5E724`
**标题**: 博弈论精讲：第1课（开场白 #内卷、空城计）
**URL**: `https://www.bilibili.com/video/BV1WBPezFE5U/...`
**platform**: custom
**custom_platform_id**: NULL
**content_status**: normal

**归属原因**: 此 item 在此前删除 CustomPlatform `bilibili` 操作后进入未分类。当时 `platform='custom'` + `custom_platform_id=NULL`。这是正确的未分类行为。

### 哔哩哔哩平台 item

**ID**: `2a40d384-d6b5-4057-baad-0acd2693dc51`
**标题**: 为什么cos比sin更自私
**URL**: `https://www.bilibili.com/video/BV1HtLz6HEEH/...`
**platform**: custom
**custom_platform_id**: c9bb75ca-... (哔哩哔哩)
**content_status**: normal

**归属原因**: 此 item 在系统来源迁移时被迁移至新创建的 CustomPlatform "哔哩哔哩"。

### 结论

两条 B站 内容身份不同：
- 未分类 item 来自旧 CustomPlatform 删除
- 哔哩哔哩平台 item 来自系统来源迁移
- 不重复，行为正确
- **未自动修改这两条记录**

---

## 七、幂等验证

第二次运行迁移：
- 待迁移系统 orphan items = 0
- 不执行任何数据库操作
- 不创建新的 CustomPlatform
- 映射 JSON 不变化

**items 从 67 到 67，custom_platforms 从 9 到 9** — 完全不变。

---

## 八、ImportService 行为

当前 `ImportService.ProcessImportAsync` 在 item 构造时检查 `SystemPlatformCustomMap.GetCustomPlatformId(rawValue)`：

- 如果映射存在：`platform = custom, custom_platform_id = UUID`
- 如果映射不存在：`platform = 检测到的系统平台, custom_platform_id = null`

迁移后新导入 YouTube/Bilibili/GitHub 内容将自动进入对应的 CustomPlatform。

---

## 九、Sidebar 行为

`HomeDataService.GetPlatformStatsAsync()` 现在只返回：
1. 每条 CustomPlatform 独立入口
2. 未分类入口

不再显示系统 Platform 入口。

---

## 十、已知未完成事项

1. **迁移前自动备份**：当前仅在首次迁移时手动创建备份，未来迁移前自动备份尚未实现
2. **媒体封面**：平台页封面显示问题留待独立阶段
3. **测试覆盖**：迁移预览、候选决策逻辑的自动化测试尚未补充

---

## 审计验收表

| 项目 | 状态 | 证据 |
|---|---|---|
| 历史首次迁移前备份 | 缺失（已记录） | 无法追溯 |
| 当前状态备份 | 已创建 | backups/post-system-platform-migration-current-state_20260616_165902 |
| 系统orphan清理 | 0条剩余 | SQL查询 |
| 孤立custom_platform_id | 0条 | SQL查询 |
| PRAGMA foreign_keys | 1 | 执行结果 |
| PRAGMA foreign_key_check | 无违规 | 执行结果 |
| PRAGMA integrity_check | ok | 执行结果 |
| 幂等验证 | 通过 | 二次运行0变更 |
| 映射文件 | raw→UUID | system_platform_custom_map.json |

---

## Part 2: 完整测试与验证

### 测试矩阵

| 测试类别 | 测试数量 | 文件 |
|---|---|---|
| MigrationBackupGateTests | 8 | MigrationBackupGateTests.cs |
| SystemPlatformMigrationFullTests | 30 | SystemPlatformMigrationFullTests.cs |
| PlatformManagementViewModelTests | 16 | PlatformManagementViewModelTests.cs |
| SystemPlatformDisplayNamesTests | 10 | SystemPlatformDisplayNamesTests.cs |
| **总计** | **64** | |

### 完整测试覆盖

**迁移成功路径**: YouTube/Bilibili/GitHub/Douyin/所有支持平台
**字段保护**: id/title/body/author/original_url/normalized_url 不变
**媒体保护**: media_assets 记录数量和字段不变
**幂等**: 第二次迁移为0条
**映射持久化**: raw→UUID JSON 正确写入
**改名稳定性**: CustomPlatform 改名后映射仍返回同一UUID
**备份**: VACUUM INTO 创建独立快照 + manifest.json
**备份失败门禁**: 阻止迁移执行
**未分类**: 删除CustomPlatform后items进入未分类
**Sidebar**: CustomPlatform-only，不显示系统Platform
**管理页**: 只显示CustomPlatform条目
**FK/integrity**: 临时数据库通过
**软删除**: 不参与迁移（deleted_at IS NOT NULL）
**删除平台**: CustomPlatform.delete 将 items 迁移到未分类
| B站两条内容解释 | 完成 | Section 六 |

---

## Part 3: 全部测试修复与稳定通过

### 测试发现数量: 426
### 通过: 426
### 失败: 0
### 跳过: 0

### 修复内容

**ListDataServiceTests (12个)**:
- 所有 `StandardPlatform == Platform.xxx` 断言改为 `s.Id == cpId`（CustomPlatform 条目查找）
- 不再断言系统平台入口存在
- 不再断言 merged count

**ImportServiceTests (25个)**:
- 注入 `FakePlatformRouter` + `FakeContentParser` 替代真实网络请求
- `ImportService` 新增 `PlatformRouter` 构造函数注入
- `PlatformRouter.GetParser` 改为 `virtual` 以支持 override
- 测试执行时间从 7分钟 降至 12秒

**测试隔离**:
- FakeParser 返回 URL-derived 内容（非固定字符串）
- 每个测试使用独立 in-memory SQLite
- 无共享状态

### 连续稳定验证
- 第1次: 426/426 通过 (12秒)
- 第2次: 426/426 通过 (12秒)

### Publish 成功
- 输出目录: `windows/src/Gatherly.Windows/bin/Release/net8.0/win-x64/publish/`
| ImportService | 写入custom+UUID | ImportService.cs line 160-165 |
| Sidebar | CustomPlatform-only | HomeDataService.cs |

---

## Part 4: 最终验收

### 数据库最终状态

| 表 | 数量 |
|---|---|
| items (active) | 38 |
| media_assets | 179 |
| custom_platforms | 9 |

### B站两条内容确认

| item ID | 标题 | platform | custom_platform_id | 归属 |
|---|---|---|---|---|
| 93E92C29-... | 博弈论精讲 | custom | NULL | 未分类（来自旧CustomPlatform删除） |
| 2a40d384-... | 为什么cos比sin更自私 | custom | c9bb75ca-... (哔哩哔哩) | 哔哩哔哩平台（来自系统来源迁移） |

两条内容身份不同，行为正确，未自动修改。

### 完整测试结果

- 第1次: 426/426 通过 (10秒)
- 第2次: 426/426 通过 (14秒)

### 数据库完整性

- PRAGMA foreign_keys = 1
- PRAGMA foreign_key_check = 无违规
- PRAGMA integrity_check = ok

### 封面验证

所有 media_assets 记录完整（179条），本地文件存在于 `%LOCALAPPDATA%\Gatherly\media\`。首页与平台页使用相同的 `LoadFirstImagePathsAsync` 封面解析逻辑。此前封面异常最可能为旧构建版本问题，当前最新 publish 无法复现。

### Publish 成功

- 输出目录: `windows/src/Gatherly.Windows/bin/Release/net8.0/win-x64/publish/`
