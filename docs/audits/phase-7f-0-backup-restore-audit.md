# Phase 7F-0C: Backup Package V2 冻结规格

> 分支: feature/phase-7f-backup-restore-v2
> HEAD: dc93455
> 审计时间: 2026-06-17
> 测试基线: 426/426

---

## 1. 当前分支与HEAD

- 分支: `feature/phase-7f-backup-restore-v2`
- HEAD: `dc934552398797dbf9864623eec99d11399ea225`
- 仅修改: `docs/audits/phase-7f-0-backup-restore-audit.md`
- 未修改生产代码

---

## 2. 平台显示名称通用格式

冻结 V2 settings 格式:

```json
{
  "platformDisplayNames": [
    { "platformRawValue": "bilibili", "displayName": "哔哩哔哩" },
    { "platformRawValue": "youtube", "displayName": "我的影片" }
  ]
}
```

规则:
- `platformRawValue` 是稳定身份
- `displayName` 只是可变显示值
- 不使用显示名称作为映射身份
- 未知 rawValue 允许保存但恢复时记录 warning
- Windows 私有 `platform_display_names.json` 不直接作为跨平台标准
- V2 导出时转换为通用格式
- Windows 恢复时从通用格式恢复回私有 JSON
- macOS 转换到 UserDefaults 或未来设置存储

---

## 3. 系统平台映射通用格式

冻结 V2 settings 格式:

```json
{
  "systemPlatformMappings": [
    { "platformRawValue": "youtube", "customPlatformId": "8b8b33d0-7696-4ba3-ac54-c93c99952428" }
  ]
}
```

规则:
- `platformRawValue` 是稳定身份
- `customPlatformId` 是目标 CustomPlatform UUID
- Windows 私有 `system_platform_custom_map.json` 不直接进入跨平台标准
- 备份时转换为通用格式
- 恢复时从通用格式重建私有 JSON
- macOS 可以保留或忽略当前不消费的映射
- 目标 CustomPlatform UUID 不存在时记录 warning，不得静默写入

---

## 4. pending.json 处理规则

发现 pending 时:
1. 尝试恢复正式映射
2. 恢复成功 → 继续备份
3. 恢复失败 → **阻止备份**，返回明确错误

原因: pending 代表应用状态不稳定，基于不稳定状态的备份不可靠。

---

## 5. 当前兼容性 vs V2 目标兼容性

### 当前产品兼容性

| 源格式 | macOS 当前 | Windows 当前 |
|---|---|---|
| macOS V1 | 完全支持 | 部分支持(空库) |
| Windows 安全备份 | 不适用 | 仅迁移用 |
| macOS V2 | 未实现 | 未实现 |
| Windows V2 | 未实现 | 未实现 |

### V2 目标兼容性

| 源格式 | macOS V2 | Windows V2 |
|---|---|---|
| macOS V1 | 需转换 | 需转换 |
| macOS V2 | 完全支持 | 完全支持(需 macOS V2 写入) |
| Windows V2 | 完全支持(需 Windows V2 写入) | 完全支持 |

前提: macOS 实现 V2 写入、通用格式转换、跨平台路径重建。

---

## 6. CustomPlatform 四类冲突冻结策略

### 相同 UUID、相同名称
- 复用目标记录
- 不生成新 UUID

### 相同 UUID、不同名称
- 完全替换模式: 使用源名称
- 合并模式: 保留目标名称，记录 warning
- 所有源 item 仍映射到原 UUID，不生成第二个 UUID

### 不同 UUID、相同名称
- 保留两个 CustomPlatform
- 不按名称合并
- 不生成第三个 UUID

### 不同 UUID、不同名称
- 正常保留两个平台

### 附加规则
- logo_path 冲突: 保留目标文件
- sort_order 冲突: 保留目标值
- systemPlatformMappings 指向: 源 UUID → 目标 UUID（不重映射）

---

## 7. Item 冲突冻结策略

### 完全替换模式
- 以源备份为准，不执行逐项合并

### 合并模式

| 场景 | 建议行为 |
|---|---|
| 相同 ID 且内容相同 | 复用目标 |
| 相同 ID 但内容不同 | 源生成新 UUID + warning |
| 不同 ID 但非空 normalized_url 相同 | 默认重复，保留目标 |
| normalized_url 为空 | 不能互相判重 |
| original_url 相同 | 次级判重依据 |
| platform_content_id 相同 | 补充判重依据 |
| 回收站状态冲突 | 保留目标状态 + warning |

不得使用 INSERT OR IGNORE 静默吞掉不同内容。

---

## 8. Folder 冲突冻结策略

| 场景 | 建议行为 |
|---|---|
| 相同 UUID 同名 | 复用 |
| 相同 UUID 不同名 | 合并模式保留目标 + warning |
| 不同 UUID 同名 | 保留两个 Folder |
| 父 Folder 重映射 | 使用映射后 UUID |
| 父 Folder 缺失 | 放入根目录 + warning |
| 循环引用 | 拒绝恢复（损坏备份） |

---

## 9. Media 冲突冻结策略

| 冲突场景 | 文件行为 | DB 记录 | 新 UUID | Warning |
|---|---|---|---|---|
| 相同 media ID + 相同 hash | 复用 | 复用 | 否 | 否 |
| 相同 media ID + 不同 hash | 新 packagePath | 新记录 | 是 | 是 |
| 相同 packagePath + 相同 hash | 复用文件 | 复用记录 | 否 | 否 |
| 相同 packagePath + 不同 hash | 源文件重命名 | 新记录 | 是 | 是 |
| 仅文件名相同、路径不同 | 不冲突 | 不冲突 | 否 | 否 |
| DB 记录存在、文件缺失 | N/A | 保留记录 + warning | 否 | 是 |
| 文件存在、DB 无记录 | 忽略 | N/A | 否 | 是 |

---

## 10. 路径碰撞策略

V2 包内路径规范:
- 全部使用 `/`
- 不得以 `/` 开头
- 不得包含盘符或 `..`
- Unicode NFC 规范化

碰撞检查:
- Unicode 规范化后碰撞 + hash 相同 → 复用
- Unicode 规范化后碰撞 + hash 不同 → 源文件重命名为 `原文件名_<短hash>.ext`
- Windows 大小写不敏感碰撞 + hash 相同 → 复用
- Windows 大小写不敏感碰撞 + hash 不同 → 重命名
- 不得覆盖任一源文件

---

## 11. 校验值冻结规则

所有打包文件进入 `manifest.files[]`:

```json
{ "path": "...", "type": "...", "size": 123, "sha256": "...", "required": true }
```

冻结规则:
- 所有文件均计算 SHA-256
- database + settings: required
- platform_logos: required
- media: 按记录标记 required/optional
- required 文件缺失或 hash 错误 → 拒绝恢复
- optional 文件缺失或 hash 错误 → warning 并继续
- manifest 不记录自身最终 SHA-256
- 本阶段不实现数字签名
- 大媒体库: 哈希计算提供进度和 CancellationToken

---

## 12. V2 最终目录树

```
backup.zip
├── manifest.json                           [必需] [通用]
├── database/
│   └── archiver.db                         [必需] [通用]
├── media/                                  [必需] [通用]
│   └── {itemUUID}/
│       ├── cover.jpg
│       └── ...
├── platform_logos/                         [必需] [通用]
│   └── {uuid}.png
└── settings/                               [必需] [通用]
    ├── platform_display_names.json         [通用]
    └── system_platform_mappings.json       [通用]
```

`system_platform_custom_map.pending.json` 明确不进入包。

---

## 13. manifest 完整字段

```json
{
  "formatVersion": 2,
  "createdAt": "2026-06-17T00:00:00Z",
  "appVersion": "1.0.0",
  "sourceApp": "Gatherly",
  "sourceOS": "windows|macos",
  "databaseSchemaVersion": 1,
  "databaseUserVersion": 3,
  "counts": {
    "items": 38,
    "mediaAssets": 179,
    "folders": 0,
    "customPlatforms": 9,
    "importTasks": 41,
    "trashRecords": 28
  },
  "files": [
    { "path": "database/archiver.db", "type": "database", "size": 671744, "sha256": "...", "required": true },
    { "path": "settings/platform_display_names.json", "type": "settings", "size": 123, "sha256": "...", "required": true },
    { "path": "settings/system_platform_mappings.json", "type": "settings", "size": 456, "sha256": "...", "required": true }
  ],
  "features": {
    "hasDatabase": true,
    "hasMedia": true,
    "hasPlatformLogos": true,
    "hasPlatformDisplayNames": true,
    "hasSystemPlatformMappings": true,
    "hasTrash": true,
    "hasFTS": true,
    "hasFolders": false
  },
  "warnings": []
}
```

冻结:
- formatVersion = 整数 2
- createdAt = ISO 8601 UTC
- 所有 count = 非负整数

---

## 14. V1/V2 兼容矩阵

### 当前兼容性

| 源格式 | macOS 当前 | Windows 当前 |
|---|---|---|
| macOS V1 (archiver.db + backup_info.json) | 完全支持 | 部分支持(空库) |
| Windows 安全备份 (VACUUM INTO 目录) | 不适用 | 仅迁移用 |

### V2 目标兼容性

| 源格式 | macOS V2 | Windows V2 |
|---|---|---|
| macOS V1 | 需转换(无manifest) | 需转换 |
| Windows V2 | 完全支持 | 完全支持 |
| macOS V2 | 完全支持 | 完全支持 |

---

## 15. 安全矩阵

| 安全项 | macOS 当前 | Windows 当前 | V2 要求 |
|---|---|---|---|
| ZIP 绝对路径拒绝 | 未实现 | 未实现 | **P0** |
| `../` 路径拒绝 | 未实现 | 未实现 | **P0** |
| 目标目录边界检查 | 未实现 | 未实现 | **P0** |
| 符号链接拒绝 | 未实现 | 未实现 | **P0** |
| Zip Bomb 限制 | 未实现 | 未实现 | **P0** |
| 最大文件数量 | 未实现 | 未实现 | P1 |
| 最大单文件大小 | 未实现 | 未实现 | P1 |
| manifest 校验 | 未实现 | 未实现 | P1 |
| SHA-256 校验 | 未实现 | 未实现 | P1 |
| 数据库完整性 | 未实现 | 未实现 | **P0** |
| foreign_key_check | 未实现 | 未实现 | **P0** |
| 恢复前安全备份 | 未实现 | 未实现 | **P0** |
| 恢复失败回滚 | 未实现 | 未实现 | **P0** |
| 临时目录清理 | 是 | 是 | 是 |

---

## 16. 风险清单

### P0 (7项)

| 编号 | 风险 | 触发 | 影响 | 阶段 |
|---|---|---|---|---|
| P0-1 | Zip 路径穿越 | Windows 解压 | 文件写入目标目录外 | 7F-2A |
| P0-2 | 符号链接逃逸 | 解压含 symlink 的 zip | 任意文件读写 | 7F-2A |
| P0-3 | Zip Bomb | 恶意/损坏 zip | 磁盘耗尽 | 7F-2A |
| P0-4 | 数据库损坏覆盖原库 | 损坏 backup.db | 原数据不可恢复 | 7F-2A |
| P0-5 | 恢复前无安全备份 | 恢复前 | 恢复失败时原库已修改 | 7F-2B |
| P0-6 | 数据库与媒体切换不一致 | 原子切换失败 | DB 与文件不同步 | 7F-2B |
| P0-7 | 恢复中途失败无回滚 | 恢复过程异常 | 部分数据丢失 | 7F-2B |

### P1 (5项)

| 编号 | 风险 | 阶段 |
|---|---|---|
| P1-1 | Phase 7E 映射丢失 | 7F-1 |
| P1-2 | INSERT OR IGNORE 静默跳过 | 7F-2C |
| P1-3 | FTS 未 rebuild | 7F-2D |
| P1-4 | 无校验值 | 7F-1 |
| P1-5 | CustomPlatform 归属断裂 | 7F-2D |

### P2 (3项)

| 编号 | 风险 | 阶段 |
|---|---|---|
| P2-1 | 无恢复进度提示 | 7F-2B |
| P2-2 | V1 格式不兼容 | 7F-2A |
| P2-3 | 大媒体库性能 | 7F-1 |

---

## 17. 测试覆盖矩阵

| 能力 | macOS 当前 | Windows 当前 | 缺失 | 阶段 |
|---|---|---|---|---|
| V1 识别 | 无 | 有 | macOS | 7F-1 |
| V2 manifest 序列化 | 无 | 无 | 全部 | 7F-1 |
| SHA-256 计算 | 无 | 无 | 全部 | 7F-1 |
| ZIP 路径穿越 | 无 | 无 | 全部 | 7F-2A |
| 符号链接 | 无 | 无 | 全部 | 7F-2A |
| Zip Bomb | 无 | 无 | 全部 | 7F-2A |
| 损坏数据库 | 无 | 无 | 全部 | 7F-2A |
| 校验错误 | 无 | 无 | 全部 | 7F-2A |
| 完全替换回滚 | 无 | 无 | 全部 | 7F-2B |
| 合并回滚 | 无 | 无 | 全部 | 7F-2C |
| Item 冲突 | 无 | 无 | 全部 | 7F-2C |
| CustomPlatform 冲突 | 无 | 无 | 全部 | 7F-2C |
| Folder 冲突 | 无 | 无 | 全部 | 7F-2C |
| Media 冲突 | 无 | 无 | 全部 | 7F-2C |
| FTS rebuild | 无 | 有 | macOS | 7F-2D |
| 重复恢复幂等 | 无 | 无 | 全部 | 7F-2C |
| 跨平台路径 | 无 | 无 | 全部 | 7F-3 |
| Phase 7E 映射 | 无 | 无 | 全部 | 7F-2D |

---

## 18. Phase 7F-1 冻结范围

### 实现
- V2 manifest 模型和 schema
- SQLite 安全快照 (VACUUM INTO)
- 通用 settings 导出 (platformDisplayNames + systemPlatformMappings)
- pending 恢复门禁
- media 和 platform_logos 打包
- 所有文件 SHA-256
- 安全 ZIP 创建
- V1/V2 格式检测器
- 备份创建服务与 UI 入口

### 不实现
- 恢复、数据库替换、冲突重映射、FTS 恢复

### 文件
- 生产: `Services/BackupServiceV2.cs`, `Services/BackupManifestV2.cs`
- 测试: `Tests/BackupServiceV2Tests.cs`
- 共享: `shared/import-export/backup-package-v2.schema.json`

---

## 19. Phase 7F-2 冻结范围

- **7F-2A**: 包验证 + 安全解压 + Zip Slip + 大小限制
- **7F-2B**: 完全替换恢复 + 恢复前备份 + 原子切换 + 回滚
- **7F-2C**: 合并导入 + ID 重映射 + 冲突策略 + staging
- **7F-2D**: Phase 7E 映射恢复 + FTS rebuild + UI 刷新

---

## 20. Phase 7F-3 冻结测试范围

macOS V1 → Windows V2 / Windows V2 → Windows V2 / macOS V2 → Windows V2
损坏 zip / 路径穿越 / 符号链接 / Zip Bomb / 缺失 manifest / 缺失数据库 / hash 错误 / 数据库损坏 / 恢复中途崩溃 / 重复恢复 / 大媒体库 / FTS 搜索

---

## 21. 验收表

| 项目 | 状态 | 证据 |
|---|---|---|
| V2 目录树冻结 | 完成 | Section 12 |
| manifest 字段冻结 | 完成 | Section 13 |
| V1/V2 兼容矩阵 | 完成 | Section 14 |
| 安全矩阵 | 完成 | Section 15 |
| CustomPlatform 冲突策略 | 完成 | Section 6 |
| Item 冲突策略 | 完成 | Section 7 |
| Folder 冲突策略 | 完成 | Section 8 |
| Media 冲突策略 | 完成 | Section 9 |
| 路径碰撞规则 | 完成 | Section 10 |
| 校验值规则 | 完成 | Section 11 |
| 设置通用格式 | 完成 | Section 2-3 |
| pending 处理 | 完成 | Section 4 |
| 风险清单 | 完成 | Section 16 |
| 测试覆盖矩阵 | 完成 | Section 17 |
| Phase 范围冻结 | 完成 | Section 18-20 |
