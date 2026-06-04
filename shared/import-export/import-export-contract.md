# 拾屿 Archiver — 跨平台导入/导出格式契约

> 本文档定义备份导出和还原导入的格式规范。
> 明确区分"当前 macOS 已实现格式"和"未来跨平台推荐格式"。

---

## 1. 当前 macOS 备份格式（已实现）

### 导出包结构

```
Archiver备份_YYYYMMDD_HHmm.zip
├── archiver.db              ← 完整 SQLite 数据库文件
├── media/                   ← 整个 media 目录
│   └── {itemUUID}/
│       ├── cover.jpg
│       ├── image_001.jpg
│       └── video.mp4
├── platform_logos/          ← 整个 platform_logos 目录
│   └── {logoFileName}
└── backup_info.json         ← 元信息
```

### backup_info.json（当前实现）

```json
{
  "version": "1.1.16",
  "backupDate": "2025-06-04T15:30:00Z",
  "hasDatabase": true,
  "hasMedia": true,
  "hasLogos": true
}
```

### 当前实现特点

- zip 文件名包含中文（`Archiver备份_`），Windows 可能有编码兼容问题
- 没有 `formatVersion` 字段
- 没有 `sourcePlatform` 字段
- 没有 item/media 计数
- 数据库文件名为 `archiver.db`（在 zip 根目录）
- 媒体文件使用相对路径 `{itemUUID}/fileName`（跨平台友好）
- 平台 Logo 直接复制整个目录

### 还原流程（当前实现）

1. 解压 zip 到临时目录
2. 验证 `archiver.db` 存在
3. SQLite `ATTACH DATABASE` + `INSERT OR IGNORE` 合并数据库
4. 复制 media 文件（只复制不存在的文件，不覆盖）
5. 复制 platform_logos（只复制不存在的文件）
6. 清理临时目录

---

## 2. 未来跨平台推荐格式

### 导出包结构

```
archiver-export-YYYYMMDD-HHmm.zip
├── manifest.json            ← 导出包元信息（替代 backup_info.json）
├── database/
│   └── archiver.sqlite      ← 数据库文件（统一文件名）
├── media/
│   └── {itemUUID}/
│       ├── cover.jpg
│       ├── image_001.jpg
│       └── video.mp4
└── custom-platforms/
    └── logos/
        └── {logoFileName}
```

### manifest.json（未来推荐）

```json
{
  "app": "Archiver",
  "formatVersion": 1,
  "exportedAt": 1717795800,
  "sourcePlatform": "macOS",
  "databaseVersion": 2,
  "containsDatabase": true,
  "containsMedia": true,
  "itemCount": 41,
  "mediaCount": 128
}
```

### 与当前格式的差异

| 维度 | 当前 macOS 实现 | 未来跨平台推荐 |
|------|----------------|--------------|
| zip 文件名 | `Archiver备份_YYYYMMDD_HHmm.zip`（含中文） | `archiver-export-YYYYMMDD-HHmm.zip`（ASCII） |
| 元信息文件 | `backup_info.json` | `manifest.json` |
| 元信息格式 | 简单 key-value | 标准化 schema（含 formatVersion、sourcePlatform） |
| 数据库路径 | zip 根目录 `archiver.db` | `database/archiver.sqlite` |
| Logo 路径 | `platform_logos/` | `custom-platforms/logos/` |

### 兼容迁移建议

1. 新版 App 应同时支持读取 `backup_info.json`（旧格式）和 `manifest.json`（新格式）
2. 还原时优先检查 `manifest.json`，不存在则 fallback 到 `backup_info.json`
3. 导出时统一使用新格式 `manifest.json`
4. 数据库文件名：还原时兼容 `archiver.db`（旧）和 `database/archiver.sqlite`（新）

---

## 3. 路径规则

### 导出包内部路径

- **必须使用相对路径**，使用 `/` 分隔符
- **禁止保存绝对路径**，例如：
  - ❌ `/Users/xxx/Library/Application Support/Archiver/media/...`
  - ❌ `C:\Users\xxx\AppData\Local\Archiver\media\...`
  - ❌ `file:///Users/xxx/...`
- 媒体文件路径格式：`{itemUUID}/fileName`（当前实现已满足）
- 路径分隔符统一为 `/`（Windows 端导入时转换为 `\`）

### 数据库中的路径

- `media_assets.local_path` 存储相对路径：`{itemUUID}/fileName`
- 这是跨平台友好的格式，应保持不变
- 导入时需要将相对路径映射到本地数据目录：
  - macOS: `~/Library/Application Support/Archiver/media/{itemUUID}/fileName`
  - Windows: `%LOCALAPPDATA%\Archiver\media\{itemUUID}\fileName`
  - 自定义目录：`{customBasePath}/media/{itemUUID}/fileName`

---

## 4. 冲突处理规则

### 当前 macOS 实现

| 冲突类型 | 当前策略 |
|----------|---------|
| 数据库记录 | `INSERT OR IGNORE`（ID 已存在则跳过） |
| 媒体文件 | 只复制不存在的文件（不覆盖） |
| 平台 Logo | 只复制不存在的文件（不覆盖） |

### 未来推荐默认策略

| 冲突类型 | 推荐策略 | 说明 |
|----------|---------|------|
| item id 已存在 | **skip** | 跳过该条记录，不覆盖 |
| normalizedURL 已存在 | **skip** | 跳过该条记录（等价内容） |
| media 文件已存在 | **skip** | 跳过文件复制（文件内容由 checksum 保证一致） |
| folder id 已存在 | **skip** | 跳过该文件夹 |
| custom platform id 已存在 | **skip** | 跳过该自定义平台 |
| schema version 不兼容 | **abort** | 中止还原，提示用户升级 App |

### 策略枚举

```json
{
  "conflictStrategies": {
    "skip": "跳过冲突项，保留现有数据（推荐默认）",
    "overwrite": "用导入数据覆盖现有数据",
    "duplicate": "保留两份，为冲突项生成新 ID",
    "merge": "合并字段（仅更新非空字段）",
    "abort": "中止整个导入操作"
  }
}
```

---

## 5. 时间字段规范

- 导出包中的时间字段使用 **Unix timestamp（秒）**
- `manifest.json` 中的 `exportedAt` 为导出时间
- 不使用 ISO 8601 字符串（避免时区歧义）
- Windows/C# 端使用 `DateTimeOffset.ToUnixTimeSeconds()` 转换

---

## 6. 数据库版本管理

- `manifest.json` 中的 `formatVersion` 表示导出包格式版本（当前为 1）
- `manifest.json` 中的 `databaseVersion` 表示数据库 schema 版本（与 GRDB migration 一致）
- 还原时应检查 `databaseVersion`：
  - 如果备份的 `databaseVersion` > 当前 App 支持的版本，应 abort 并提示升级
  - 如果备份的 `databaseVersion` <= 当前版本，正常还原

---

## 7. 跨平台导出注意事项

### 文件名编码

- 当前 zip 文件名 `Archiver备份_YYYYMMDD_HHmm.zip` 包含中文
- Windows 系统对中文文件名支持有限（取决于代码页设置）
- 未来跨平台导出建议使用 ASCII 文件名：`archiver-export-YYYYMMDD-HHmm.zip`

### 数据库文件名

- 当前：`archiver.db`
- 推荐：`database/archiver.sqlite`
- 还原时应同时兼容两种文件名

### 媒体文件路径

- 当前存储格式 `{itemUUID}/fileName` 是跨平台友好的
- 保持不变，不需要修改

---

## 8. JSON Schema 文件说明

| Schema 文件 | 说明 |
|-------------|------|
| `backup-package.schema.json` | 整个导出包的目录结构定义 |
| `manifest.schema.json` | `manifest.json` 的字段定义（未来格式） |
| `item-export.schema.json` | 导出时 Item 的字段定义 |
| `folder-export.schema.json` | 导出时 Folder 的字段定义 |
| `media-export.schema.json` | 导出时 MediaAsset 的字段定义（含包内路径） |

---

## 9. ParsedContent（运行时模型）

`ParsedContent` 不参与导入导出，仅作为解析器输出。Windows 端解析器应输出等价结构，由导入服务转换为持久化模型。详见 `shared/model/model-contract.md`。
