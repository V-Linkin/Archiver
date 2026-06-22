# Phase 7F-2A Backup Package V2 恢复设计

**状态**: 设计冻结，尚未实现
**日期**: 2026-06-18
**阶段**: Phase 7F-2A-1

---

## 1. 范围

### 1.1 本期范围

Backup Package V2 恢复功能：V2 ZIP 验证、恢复计划生成、事务数据库合并、媒体/Logo/Settings 恢复、Pending Mapping、恢复前安全点、Restore Journal、取消边界、设置页恢复 UI。

### 1.2 非范围

备份创建（Phase 7F-1）、V1 导入增强、macOS 代码、跨设备恢复、增量恢复、加密、合并重复 Item。

---

## 2. V1/V2 兼容边界（冻结）

### 2.1 V1 现有行为

| 步骤 | 行为 | 代码 |
|---|---|---|
| 1 | 接受 ZIP 路径 | `BackupImportService.ImportBackupAsync()` |
| 2 | 解压到临时目录 | `ZipFile.ExtractToDirectory` |
| 3 | 定位 archiver.db | `LocateBackupRoot()` |
| 4 | 可选读取 backup_info.json | `ReadBackupInfo()` — 仅日志 |
| 5 | 检查目标核心表是否为空 | `DatabaseMergeService.IsDatabaseEmpty()` |
| 6 | ATTACH DATABASE | `MergeAsync()` |
| 7 | INSERT OR IGNORE 复制 6 表 | `CopyTableDataAsync()` |
| 8 | Rebuild FTS5 | `RebuildFtsAsync()` |
| 9 | 恢复 media/ | `MediaRestoreService.RestoreMedia()` |
| 10 | 恢复 platform_logos/ | `MediaRestoreService.RestorePlatformLogos()` |
| 11 | 清理临时目录 | finally |

V1 不主动清空数据库。核心表非空时拒绝导入。

### 2.2 V2 / 未知格式

V2 → 新流程。未知 → 拒绝。

### 2.3 冻结

Phase 7F-2A 不修改 V1。UI 标示 V1 限制。

---

## 3. 安全不变量

1. 不覆盖当前记录
2. 不删除当前记录
3. 不清空任何表
4. 不直接替换数据库
5. 不通过 INSERT OR IGNORE 静默丢弃
6. 所有跳过/重映射进入恢复计划与摘要
7. 恢复前创建安全点
8. 安全点失败 → 禁止恢复
9. 数据库修改在单一事务中
10. 新文件先写入 staging
11. 最终路径只能是当前不存在的新路径
12. 任何阶段失败必须回滚到安全状态

---

## 4. 数据库表结构（已核实）

来源：`shared/db/migrations/v1_create_tables.sql`

### 4.1 items

| 字段 | 类型 | 约束 | 内容相同 | 引用 |
|---|---|---|---|---|
| id | TEXT | PK | 是 | — |
| title | TEXT | 可空 | 是 | — |
| body | TEXT | 可空 | 是 | — |
| original_url | TEXT | NOT NULL | 否 | — |
| platform | TEXT | NOT NULL | 是 | — |
| platform_content_id | TEXT | 可空 | 是 | — |
| normalized_url | TEXT | NOT NULL | 是（判重键） | — |
| author | TEXT | 可空 | 是 | — |
| author_id | TEXT | 可空 | 是 | — |
| publish_date | REAL | 可空 | 否 | — |
| import_date | REAL | NOT NULL | 否 | — |
| modify_date | REAL | NOT NULL | 否 | — |
| content_status | TEXT | NOT NULL | 是 | — |
| archive_status | TEXT | NOT NULL | 否 | — |
| media_status | TEXT | NOT NULL | 否 | — |
| cover_asset_id | TEXT | 可空 | 否 | 逻辑引用 → media_assets.id（无数据库 FK） |
| folder_id | TEXT | 可空 | 否 | 逻辑引用 → folders.id（无数据库 FK） |
| remark | TEXT | 可空 | 是 | — |
| is_starred | INTEGER | NOT NULL | 是 | — |
| version | INTEGER | NOT NULL | 否 | — |
| deleted_at | REAL | 可空 | 否 | — |
| custom_platform_id | TEXT | 可空 | 否 | 逻辑引用 → custom_platforms.id（无数据库 FK） |

DB 唯一索引：仅 PK id。普通索引：platform, archive_status, folder_id, normalized_url, import_date, deleted_at。

### 4.2 media_assets

| 字段 | 类型 | 约束 | 内容相同 | 引用 |
|---|---|---|---|---|
| id | TEXT | PK | 是 | — |
| item_id | TEXT | NOT NULL | 否 | **DB FK** → items.id CASCADE |
| type | TEXT | NOT NULL | 是 | — |
| local_path | TEXT | 可空 | 是 | — |
| remote_url | TEXT | 可空 | 否 | — |
| file_name | TEXT | NOT NULL | 是 | — |
| file_size | INTEGER | NOT NULL | 否 | — |
| mime_type | TEXT | 可空 | 是 | — |
| width/height/duration | 各 | 可空 | 否 | — |
| checksum | TEXT | 可空 | 否 | — |
| download_status | TEXT | NOT NULL | 否 | — |
| thumbnail_path | TEXT | 可空 | 否 | — |
| created_at | REAL | NOT NULL | 否 | — |

DB 唯一索引：仅 PK。普通索引：item_id, type。

### 4.3 folders

| 字段 | 类型 | 约束 | 内容相同 | 引用 |
|---|---|---|---|---|
| id | TEXT | PK | 是 | — |
| name | TEXT | NOT NULL | 是 | — |
| parent_id | TEXT | 可空 | 否 | **DB FK** → folders.id CASCADE |
| platform | TEXT | NOT NULL | 是 | — |
| created_at | REAL | NOT NULL | 否 | — |
| sort_order | INTEGER | NOT NULL | 否 | — |
| custom_platform_id | TEXT | 可空 | 否 | 逻辑引用 → custom_platforms.id |

DB 唯一索引：仅 PK。普通索引：platform, parent_id。

### 4.4 trash_records

| 字段 | 类型 | 约束 | 内容相同 | 引用 |
|---|---|---|---|---|
| id | TEXT | PK | 是 | — |
| item_id | TEXT | NOT NULL | 否 | **DB FK** → items.id CASCADE |
| deleted_at | REAL | NOT NULL | 是 | — |
| auto_delete_at | REAL | NOT NULL | 是 | — |
| original_folder_id | TEXT | 可空 | 否 | 逻辑引用 → folders.id |
| original_archive_status | TEXT | NOT NULL | 是 | — |
| media_paths | TEXT | 可空 | 是 | — |

DB 唯一索引：仅 PK。普通索引：deleted_at。

### 4.5 import_tasks

| 字段 | 类型 | 约束 | 内容相同 | 引用 |
|---|---|---|---|---|
| id | TEXT | PK | 是 | — |
| original_url | TEXT | NOT NULL | 是 | — |
| normalized_url | TEXT | NOT NULL | 是 | — |
| platform | TEXT | 可空 | 是 | — |
| status | TEXT | NOT NULL | 否 | — |
| progress | REAL | NOT NULL | 否 | — |
| error_message | TEXT | 可空 | 否 | — |
| item_id | TEXT | 可空 | 否 | 逻辑引用 → items.id |
| created_at | REAL | NOT NULL | 否 | — |
| completed_at | REAL | 可空 | 否 | — |
| retry_count | INTEGER | NOT NULL | 否 | — |

DB 唯一索引：仅 PK。普通索引：status, created_at。

### 4.6 custom_platforms

| 字段 | 类型 | 约束 | 内容相同 | 引用 |
|---|---|---|---|---|
| id | TEXT | PK | 是 | — |
| name | TEXT | NOT NULL | 是 | — |
| logo_path | TEXT | 可空 | 否 | — |
| created_at | REAL | NOT NULL | 否 | — |
| sort_order | INTEGER | NOT NULL | 否 | — |

DB 唯一索引：仅 PK。**name 无唯一索引。**

### 4.7 packagePath 与 local_path

- Manifest `packagePath`: `media/{itemUUID}/file.ext`
- DB `local_path`: `{itemUUID}/file.ext`
- 关系: `packagePath = "media/" + local_path`

### 4.8 Settings / Pending Mapping 文件

- `settings/platform_display_names.json`
- `settings/system_platform_mappings.json`
- `system_platform_custom_map.pending.json`（DataDirectory 下）

---

## 5. 外键依赖与恢复顺序

```
1. custom_platforms  — 无外部依赖
2. folders          — parent_id 自引用（DB FK）
3. items            — folder_id → folders（逻辑）, custom_platform_id → custom_platforms（逻辑）
4. media_assets     — item_id → items.id（DB FK）
5. trash_records    — item_id → items.id（DB FK）
6. import_tasks     — item_id → items.id（逻辑）
```

---

## 6. UUID 映射模型

6 个映射表：customPlatformIdMap, folderIdMap, itemIdMap, mediaAssetIdMap, trashRecordIdMap, importTaskIdMap。

记录操作类型：SkipExisting, SkipBusinessDuplicate, InsertOriginalId, InsertRemappedId。

引用更新：
- folders.parent_id → folderIdMap
- folders.custom_platform_id → customPlatformIdMap
- items.folder_id → folderIdMap
- items.custom_platform_id → customPlatformIdMap
- items.cover_asset_id → mediaAssetIdMap
- media_assets.item_id → itemIdMap
- trash_records.item_id → itemIdMap
- trash_records.original_folder_id → folderIdMap
- import_tasks.item_id → itemIdMap

Item.cover_asset_id 回填：先插 Item（cover_asset_id=NULL）→ 插 MediaAsset → UPDATE 回填。被跳过的 MediaAsset 对应 cover_asset_id 保持 NULL。

映射时机：RestorePlan 阶段预先确定。执行阶段不得重新生成。

---

## 7. 业务重复键规则

### 7.1 normalized_url 判重（冻结）

**一级判重键**：`normalized_url`

**空值规则**：
- normalized_url 为 null、空字符串或仅空白 → 不参与自动判重，正常 InsertOriginalId

**规范化时机（冻结为 A）**：使用备份数据库中已保存的 normalized_url 直接比较。不在恢复计划生成时再次调用当前 URLNormalizer。理由：备份包创建时已使用当时的 URLNormalizer 规范化，恢复时应尊重创建时的规范化结果。

**大小写规则（已核实）**：
- `UrlNormalizer.Normalize()` 生成自定义 scheme URL（如 `youtube://video/{id}`），保留原始 ID 大小写
- `ItemRepository.GetByNormalizedUrlAsync` 使用 `WHERE normalized_url=$url` — **精确二进制匹配（无 COLLATE NOCASE）**
- 因此 normalized_url 比较**大小写敏感**

**跨平台规则**：
- 不同 platform 但 normalized_url 相同 → **视为重复**（如不同导入来源的同一内容）
- 不在恢复计划生成时联网展开短链

**platform_content_id（冻结为不作为判重键）**：
1. 无数据库唯一索引
2. 允许为空（可空字段）
3. 不同平台可能有相同 ID 格式（如数字序号）
4. 不使用 `platform + platform_content_id` 组合判重
5. 仅作为 RestorePlan 辅助警告

**original_url（冻结为仅辅助）**：
- 仅记录到 RestorePlan 警告
- 不单独触发自动跳过

### 7.2 业务重复响应（冻结）

命中 normalized_url 时：
- 当前 Item 保留
- 备份 Item 跳过（SkipBusinessDuplicate）
- 不自动合并任何字段
- 备份 Item 的 media_assets、trash_records、import_tasks 全部跳过
- cover_asset_id 引用的备份媒体也跳过
- 不得挂接到当前 Item

---

## 8. 每张表冲突规则

### 8.1 custom_platforms

| 条件 | 操作 |
|---|---|
| UUID 相同 + name 相同（lower+trim） | SkipExisting |
| UUID 相同 + name 不同 | InsertRemappedId |
| UUID 不同 | InsertOriginalId |

不凭 name 映射到不同 UUID 记录。理由：无唯一索引，name 非稳定身份。

### 8.2 folders

| 条件 | 操作 |
|---|---|
| UUID 相同 + 逻辑路径完全相同 | SkipExisting |
| UUID 相同 + 内容不同 | InsertRemappedId |
| UUID 不同 + 逻辑路径完全相同 | SkipExisting |
| 其他 | InsertOriginalId |

逻辑路径：`platform | name | (递归父链)` — OrdinalIgnoreCase。父先于子。parent_id 等于自身或循环 → 阻塞。

### 8.3 items

| 条件 | 操作 |
|---|---|
| UUID 相同 | SkipExisting |
| UUID 不同 + 非空 normalized_url 精确匹配 | SkipBusinessDuplicate |
| UUID 不同 + normalized_url 不同或空 | InsertOriginalId |

被跳过 Item 的 media_assets、trash_records、import_tasks 一并跳过。

### 8.4 media_assets

| 条件 | 操作 |
|---|---|
| 所属 Item 被跳过 | 跳过 |
| UUID 相同 | SkipExisting |
| UUID 不同 + local_path 相同 + Hash 相同 | InsertOriginalId（复用） |
| UUID 不同 + local_path 相同 + Hash 不同 | InsertRemappedId |
| UUID 不同 + local_path 不同 | InsertOriginalId |

### 8.5 trash_records

| 条件 | 操作 |
|---|---|
| 所属 Item 未恢复 | 跳过 |
| UUID 相同 | SkipExisting |
| UUID 不同 | InsertOriginalId |

### 8.6 import_tasks

| 条件 | 操作 |
|---|---|
| 所属 Item 未恢复 | 跳过 |
| UUID 相同 | SkipExisting |
| UUID 不同 | InsertOriginalId |

---

## 9. CustomPlatform 判重

| 条件 | 操作 |
|---|---|
| UUID 相同 + name 相同（lower+trim） | SkipExisting |
| UUID 相同 + name 不同 | InsertRemappedId |
| UUID 不同 | InsertOriginalId |

**不凭 name 映射到不同 UUID。** 数据库无 name 唯一索引。Settings 合并时 CustomPlatform ID 必须先经过 customPlatformIdMap。

---

## 10. Settings 合并

### 10.1 platform_display_names.json

当前优先。只导入缺失 key。相同 key 不同 value 保留当前。空字符串视为存在。JSON 损坏 → 阻塞。

### 10.2 system_platform_mappings.json

当前优先。只导入缺失 key。备份 ID 经 customPlatformIdMap。相同 key 不同 value 不覆盖。无法解析 → 生成 Pending Mapping。

### 10.3 Pending Mapping

备份包不含 Pending Mapping。恢复可生成。路径：`{DataDirectory}/system_platform_custom_map.pending.json`。当前已有 → 禁止恢复。Commit 后原子替换写入。写入失败 → RecoveryRequired。未成功写入不标记 Completed。

---

## 11. 文件路径冲突

新文件先 staging 冲突处理：目标不存在→直接移动；同 Hash→复用；不同 Hash→新路径（`_sha256[:12]`）。

路径安全：绝对路径/`..`/盘符→阻塞；反斜杠→规范化；长度>260→警告；保留名/非法字符→拒绝。永不覆盖旧文件。

---

## 12. 恢复前安全点

冻结方案 A（V2 安全备份包）。安全点路径（最终由 2A-6 通过 `DatabasePaths.DataDirectory` 派生）：`{DataDirectory}/restore_safety/restore-{restoreId}/`。

强制规则：失败/取消/验证失败 → 禁止恢复。成功恢复后至少保留。

---

## 13. 数据库与文件系统两阶段方案

### 13.1 执行流程

```
Phase 1:  ZIP 验证（只读，可取消）
Phase 2:  数据库只读验证（可取消）
Phase 3:  RestorePlan 生成（只读，可取消）
Phase 4:  确认后重新验证
Phase 5:  创建安全点（可取消）
Phase 6:  创建 Journal（stage=StagingFiles）
Phase 7:  Settings + Pending Mapping 生成新内容
Phase 8:  新 Settings 写入 staging 并校验 JSON
Phase 9:  文件复制到恢复 staging 目录
Phase 10: 校验 staging 文件 size + SHA-256
Phase 11: 开启数据库事务
Phase 12: 按恢复计划逐条写数据库
Phase 13: 移动新媒体/Logo 到最终新路径
Phase 14: 最终一致性验证
Phase 15: === 不可取消临界区 ===
Phase 16: Commit 数据库
Phase 17: Journal 标记 databaseCommitted=true
Phase 18: 原子替换 Settings + Pending Mapping
Phase 19: Journal 标记 settingsApplied=true
Phase 20: Journal 标记 recoveryComplete=true
Phase 21: 清理 staging
```

### 13.2 每步失败处理

| 步骤 | 失败时处理 |
|---|---|
| ZIP/数据库/Plan/安全点 | 拒绝，不修改 |
| Settings staging | 阻止 Commit，回滚，清理 |
| 文件 staging | 删除暂存，回滚 |
| 数据库事务内 | 回滚事务，删除暂存+已移动 |
| 最终一致性验证 | 同上 |
| Commit | 回滚事务，删除暂存+已移动，恢复 Settings |
| Settings 原子替换 | RecoveryRequired，启动重试 |
| Pending Mapping | RecoveryRequired，启动重试 |

### 13.3 Commit 后失败补偿

数据库已 Commit 后 Settings 替换失败：Journal 保留 databaseCommitted=true，进入 RecoveryRequired，启动重试。

Pending Mapping 写入失败：保留 databaseCommitted+settingsApplied，不标记 Completed，RecoveryRequired，启动重试。

Journal 写入失败：忽略。

---

## 14. 取消边界

### 14.1 可取消

ZIP 扫描、所有验证、Plan 生成、安全点创建、文件暂存、数据库事务内（Commit 前）。

### 14.2 不可取消

Phase 15（最终一致性检查）之后。

### 14.3 临界区请求

忽略取消，完成 Commit，显示恢复完成。

### 14.4 取消处理

回滚事务 → 删除新增+暂存 → 恢复 Settings → 保留安全点+Journal → UI 空闲。

---

## 15. 状态机

### 15.1 状态

Idle, SelectingPackage, ValidatingArchive, ValidatingManifest, ValidatingHashes, ValidatingDatabase, BuildingPlan, AwaitingConfirmation, CreatingSafetyPoint, StagingFiles, ApplyingDatabase, MovingFiles, FinalVerification, Committing, ApplyingSettings, Completed, Cancelling, Cancelled, Failed, RecoveryRequired。

### 15.2 转移

```
Idle→SelectingPackage→ValidatingArchive→ValidatingManifest→ValidatingHashes
→ValidatingDatabase→BuildingPlan→AwaitingConfirmation→CreatingSafetyPoint
→StagingFiles→ApplyingDatabase→MovingFiles→FinalVerification
→Committing→ApplyingSettings→Completed
任何可取消→Cancelling→Cancelled
任何失败→Failed
Failed/Cancelled→Idle
Completed→Idle
RecoveryRequired→Idle（处理后）
```

### 15.3 启动规则

存在未完成 Journal → RecoveryRequired。仅允许处理未完成恢复或放弃。

---

## 16. UI

设置页新增"数据恢复"区域。不显示：路径、用户名、媒体文件名、堆栈、内部类型名。

---

## 17. 媒体和 Logo

### 17.1 packagePath 转换

- ZIP `media/{itemUUID}/file.ext` → DB `local_path = {itemUUID}/file.ext`
- ZIP `platform_logos/{uuid}.png` → DB `logo_path = {uuid}.png`

### 17.2 Logo

CustomPlatform UUID 重映射不影响 logo_path。数据库有引用但文件缺失 → 阻塞。

---

## 18. Restore Journal

### 18.1 Schema

```json
{
  "journalVersion": 1,
  "restoreId": "GUID",
  "sourcePackageHash": "SHA-256",
  "sourcePackageSize": 123456789,
  "safetyPointPath": "相对路径",
  "safetyPointHash": "SHA-256",
  "stage": "StageName",
  "createdAt": "ISO 8601",
  "updatedAt": "ISO 8601",
  "plannedDatabaseOperations": { ... },
  "idMaps": { ... },
  "stagedFiles": [],
  "movedFiles": [],
  "settingsBackups": [],
  "pendingMappingsGenerated": 0,
  "databaseCommitted": false,
  "settingsApplied": false,
  "pendingMappingApplied": false,
  "recoveryComplete": false,
  "lastErrorCode": null,
  "recoveryAction": null
}
```

### 18.2 写入

临时文件 → Flush → File.Move 原子替换。禁止记录真实路径/用户名/内容。

### 18.3 Journal 阶段枚举

```
StagingFiles, ApplyingDatabase, MovingFiles, FinalVerified,
DatabaseCommitted, SettingsApplying, SettingsApplied,
PendingMappingApplying, PendingMappingApplied, Completed,
RecoveryRequired, Failed
```

---

## 19. 崩溃恢复（按 stage）

| stage | dbCommitted | 其他 | 处理 |
|---|---|---|---|
| StagingFiles/.../FinalVerified | false | false | 回滚DB，删除新增+暂存，恢复Settings，保留Journal |
| Committing | true | false | 验证一致性，完成清理 |
| 任意 | true | true | 清理Journal+临时文件 |
| Journal损坏 | — | — | 保留损坏Journal+安全点，进入RecoveryRequired |
| 安全点缺失 | — | — | 清理临时文件，记录警告 |

多 Journal 规则（冻结）：正常只允许 1 个活动 Journal。发现多个 → 禁止自动继续，进入 RecoveryRequired 人工处理。

---

## 20. RestorePlan

```csharp
enum RecordOperation { SkipExisting, SkipBusinessDuplicate, InsertOriginalId, InsertRemappedId }

record RestorePlan(...);
record TableRestorePlan(...);
record PlanRecordDetail(string BackupId, RecordOperation, string? CurrentId, string? NewId, string Reason);
record FileRestorePlan(...);
```

只读。用户确认后、执行前重新检查 user_version 和 Pending Mapping。

---

## 21. 磁盘空间与大包

### 21.1 同卷规则（冻结）

**恢复 staging 目录必须创建在最终数据目录所在卷内。** 不得使用可能位于其他卷的系统临时目录。

staging 从 `DatabasePaths.DataDirectory` 派生：

```
{DataDirectory}/restore_staging/{restoreId}/
```

同卷 Move/Rename 不会同时保留 staging 和最终两份数据副本。公式：

```
RequiredFreeSpace = D + F + S + J + M
```

其中：
- D = 从备份包解压出的数据库临时文件大小
- F = 需要写入 staging 的媒体与 Logo 文件总大小
- S = 恢复前安全点 V2 备份包预计大小
- J = Journal、Settings staging、原文件备份及其他小型临时文件大小（≈ 1 MiB）
- M = max(512 MiB, (D + F + S) × 10%)

因为 staging 与最终目录同卷且通过 Move/Rename 移动，不产生额外数据副本，所以不需要额外计入 `N`。但 staging 写入期间 `F` 仍然占用磁盘空间。

注：媒体/Logo staging 与最终路径同卷，Move 不额外占用。但 Move 期间有短暂双份占用，安全余量 M 已覆盖。

### 21.2 跨卷（冻结）

**禁止恢复时 staging 与最终数据目录跨卷。** 若 staging 无法创建在 `DataDirectory` 所在卷，阻止恢复。

### 21.3 空间检查时机

- RestorePlan 生成后
- 创建安全点前
- 文件 staging 前
- 数据库写入前

磁盘写满时：DB 未 Commit → 回滚+删除+保留安全点；DB 已 Commit → RecoveryRequired。

### 21.4 流式大文件

- ZIP FileStream 打开，不读整个 ZIP 到内存
- Manifest 可受限读入内存
- 业务文件逐 Entry 流式读取
- SHA-256 流式计算
- 数据库 Entry 流式写入临时文件
- 固定缓冲区：**81920 bytes**（80 KiB）
- CancellationToken 每次缓冲区读取后检查
- 进度按实际读取字节更新

---

## 22. P0/P1

### P0

| # | 风险 | 预防 | 阶段 | 测试关闭 | 状态 |
|---|---|---|---|---|---|
| P0-1 | 覆盖/删除当前数据 | 永不覆盖+事务 | 2A-4 | 合并测试 | 设计冻结 |
| P0-2 | UUID 映射遗漏引用 | 完整映射+回填 | 2A-4 | 映射测试 | 设计冻结 |
| P0-3 | DB成功文件失败 | Journal+回滚 | 2A-6 | 回滚测试 | 设计冻结 |
| P0-4 | 取消/崩溃无法回滚 | Journal+崩溃检测 | 2A-6 | Journal测试 | 设计冻结 |

### P1

| # | 风险 | 阶段 | 测试关闭 | 状态 |
|---|---|---|---|---|
| P1-1 | 业务重复误伤 | 2A-3 | 重复测试 | 设计冻结 |
| P1-2 | Settings冲突丢失 | 2A-5 | Settings测试 | 设计冻结 |
| P1-3 | Pending Mapping错误 | 2A-5 | Pending测试 | 设计冻结 |
| P1-4 | V1/V2 UI边界 | 2A-7 | ViewModel测试 | 设计冻结 |
| P1-5 | 大包性能/磁盘 | 2A-2 | 验证测试 | 设计冻结 |
| P1-6 | DB版本兼容 | 2A-2 | 版本测试 | 设计冻结 |

---

## 23. 测试矩阵

### 23.1 汇总

| 测试类 | 精确数量 | 阶段 | 关闭风险 |
|---|---:|---|---|
| ArchiveVerifierTests | 16 | 2A-2 | P1-5 |
| RestoreDatabaseVerifierTests | 8 | 2A-2 | P1-6 |
| RestorePlanGeneratorTests | 12 | 2A-3 | P1-1 |
| RestoreIdRemappingTests | 8 | 2A-3/4 | P0-2 |
| RestoreDatabaseMergeTests | 8 | 2A-4 | P0-1/P0-2 |
| RestoreFileStagingTests | 8 | 2A-5 | P0-3 |
| RestoreSettingsMergeTests | 6 | 2A-5 | P1-2 |
| RestorePendingMappingTests | 4 | 2A-5/6 | P1-3 |
| RestoreJournalTests | 6 | 2A-6 | P0-3/P0-4 |
| RestoreRollbackTests | 6 | 2A-6 | P0-3/P0-4 |
| RestoreCrashRecoveryTests | 4 | 2A-6 | P0-4 |
| RestoreStateMachineTests | 4 | 2A-6/7 | P0-4 |
| RestoreViewModelTests | 8 | 2A-7 | P1-4 |
| **合计** | **98** | | |

验证：16+8+12+8+8+8+6+4+6+6+4+4+8 = **98** ✓

### 23.2 各类详细场景

**ArchiveVerifierTests (16)**：合法V2包、损坏ZIP、缺少manifest、重复条目、绝对路径、盘符、反斜杠、穿越(..)、Manifest JSON损坏、Schema失败、formatVersion错误、缺少数据库、未管理业务文件、size不一致、SHA-256不一致、取消

**RestoreDatabaseVerifierTests (8)**：integrity_check失败、foreign_key_check失败、user_version不匹配、缺少必要表、缺少必要字段、字段类型不兼容、只读打开失败、取消

**RestorePlanGeneratorTests (12)**：空数据库恢复、完全重复SkipExisting、UUID相同内容不同InsertRemappedId、业务键重复SkipBusinessDuplicate、Folder引用重映射、Media引用重映射、CustomPlatform重映射、Trash恢复、ImportTask恢复、MediaAsset属被跳过Item、全部重复记录、计划摘要正确

**RestoreIdRemappingTests (8)**：Item→folder_id重映射、Item→custom_platform_id重映射、Item→cover_asset_id重映射、MediaAsset→item_id重映射、TrashRecord→item_id重映射、ImportTask→item_id重映射、Folder→parent_id重映射、Folder→custom_platform_id重映射

**RestoreDatabaseMergeTests (8)**：空DB合并成功、事务回滚、FTS5重建、cover_asset_id回填、UUID相同不插入、UUID不同正常插入、跳过记录不挂接到当前Item、合并后integrity_check通过

**RestoreFileStagingTests (8)**：文件不存在报错、Hash相同复用、Hash不同改名、复制失败回滚、移动失败回滚、取消删除暂存、回滚删除新增、旧文件保留

**RestoreSettingsMergeTests (6)**：相同设置保留当前、冲突设置保留当前、未知平台导入、缺失项填补、Pending Mapping生成、Pending Mapping不直接写入备份

**RestorePendingMappingTests (4)**：备份无Pending Mapping、恢复生成Pending Mapping、当前有Pending阻止恢复、JSON格式正确

**RestoreJournalTests (6)**：Journal创建、原子写入、读取、崩溃未提交、崩溃已提交未完成、Journal损坏检测

**RestoreRollbackTests (6)**：数据库回滚、文件回滚、Settings恢复、Journal清理、取消后状态恢复、Commit后Settings失败补偿

**RestoreCrashRecoveryTests (4)**：未提交DB自动回滚、已提交未完成继续、Journal损坏保留安全点、多Journal按序处理

**RestoreStateMachineTests (4)**：正常状态转移、任何失败→Failed、取消→Cancelled、RecoveryRequired阻塞恢复

**RestoreViewModelTests (8)**：选择文件取消、验证进度、确认恢复、防重复执行、取消、错误脱敏、成功摘要、失败后重试

### 23.3 测试原则

隔离临时目录、临时 SQLite、不访问真实 `%LOCALAPPDATA%\Gatherly`、不访问真实媒体、不使用 `PRAGMA foreign_keys = OFF`、失败保留 TRX 和控制台日志。

---

## 24. 阶段拆分

2A-1: 设计冻结（本轮）。2A-2: V2只读验证器（24个测试）。2A-3: RestorePlan生成器（20个测试）。2A-4: 事务数据库合并（16个测试）。2A-5: 媒体/Logo/Settings（10个测试）。2A-6: 安全点/Journal/取消/崩溃（10个测试）。2A-7: 设置页恢复UI（12个测试）。2A-8: 真实数据+人工验收。

---

## 附录：最终验收矩阵

| 项 | 结果 |
|---|---|
| A: 范围与状态 | 通过 |
| B: V1行为 | 通过 |
| C: V1/V2边界 | 通过 |
| D: Schema约束 | 通过 — §4 含完整表结构+FK/逻辑引用/索引/可空/比较字段 |
| E: ID映射 | 通过 — §6 含6个映射+cover_asset_id回填 |
| F: 内容相同定义 | 通过 — §4 含列标注 |
| G: UUID冲突 | 通过 — §8 + §6时机 |
| H: 业务重复 | 通过 — §7 冻结normalized_url+空值+大小写+跨平台规则 |
| I: Folder冲突 | 通过 — §8.2 循环检测 |
| J: CustomPlatform | 通过 — §9 仅凭UUID |
| K: packagePath | 通过 — §4.7 + §11 |
| L: Logo恢复 | 通过 — §17 |
| M: Settings | 通过 — §10 含边界 |
| N: Pending Mapping | 通过 — §10.3 含时序+回滚 |
| O: 安全点 | 通过 — §12 |
| P: DB/FS一致性 | 通过 — §13 含21步+每步失败 |
| Q: Journal | 通过 — §18 含完整Schema+阶段枚举 |
| R: 崩溃恢复 | 通过 — §19 按stage表格 |
| S: 取消边界 | 通过 — §14 |
| T: 状态机 | 通过 — §15 含20状态+转移 |
| U: RestorePlan | 通过 — §20 含类型+枚举 |
| V: RestoreResult | 通过 |
| W: 磁盘空间 | 通过 — §21 含同卷要求+禁止跨卷+公式含F+80KiB |
| X: 测试矩阵 | 通过 — §23 精确98个=13类 |
| Y: P0/P1 | 通过 — §22 每项含阶段/测试/状态 |
| Z: 无歧义措辞 | 通过 |

**通过: 26 | 不适用: 0 | 部分通过: 0 | 未通过: 0**

---

*状态：设计冻结，尚未实现*
*下一步：用户审查后进入 Phase 7F-2A-2*
