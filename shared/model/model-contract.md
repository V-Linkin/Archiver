# 拾屿 Archiver — 跨平台模型契约

> 本文档定义 macOS 和 Windows 共享的数据模型规范。
> 所有枚举 rawValue、字段名、数据库列名不可随意更改。

---

## 1. Swift Model ↔ SQLite 表映射

| Swift Model | SQLite 表 | SQL 文件 |
|-------------|-----------|----------|
| `Item` | `items` | `shared/db/migrations/v1_create_tables.sql` |
| `Folder` | `folders` | `shared/db/migrations/v1_create_tables.sql` |
| `MediaAsset` | `media_assets` | `shared/db/migrations/v1_create_tables.sql` |
| `ImportTask` | `import_tasks` | `shared/db/migrations/v1_create_tables.sql` |
| `TrashRecord` | `trash_records` | `shared/db/migrations/v1_create_tables.sql` |
| `CustomPlatform` | `custom_platforms` | `CustomPlatformRepository.swift`（独立建表） |

---

## 2. Swift 字段名 ↔ SQLite 列名映射

### Item → items

| Swift 字段名 | SQLite 列名 |
|-------------|------------|
| `id` | `id` |
| `title` | `title` |
| `body` | `body` |
| `originalURL` | `original_url` |
| `platform` | `platform` |
| `platformContentID` | `platform_content_id` |
| `normalizedURL` | `normalized_url` |
| `author` | `author` |
| `authorID` | `author_id` |
| `publishDate` | `publish_date` |
| `importDate` | `import_date` |
| `modifyDate` | `modify_date` |
| `contentStatus` | `content_status` |
| `archiveStatus` | `archive_status` |
| `mediaStatus` | `media_status` |
| `coverAssetID` | `cover_asset_id` |
| `folderID` | `folder_id` |
| `customPlatformID` | `custom_platform_id` |
| `remark` | `remark` |
| `isStarred` | `is_starred` |
| `version` | `version` |
| `deletedAt` | `deleted_at` |

### Folder → folders

| Swift 字段名 | SQLite 列名 |
|-------------|------------|
| `id` | `id` |
| `name` | `name` |
| `parentID` | `parent_id` |
| `platform` | `platform` |
| `customPlatformID` | `custom_platform_id` |
| `createdAt` | `created_at` |
| `sortOrder` | `sort_order` |

### MediaAsset → media_assets

| Swift 字段名 | SQLite 列名 |
|-------------|------------|
| `id` | `id` |
| `itemID` | `item_id` |
| `type` | `type` |
| `localPath` | `local_path` |
| `remoteURL` | `remote_url` |
| `fileName` | `file_name` |
| `fileSize` | `file_size` |
| `mimeType` | `mime_type` |
| `width` | `width` |
| `height` | `height` |
| `duration` | `duration` |
| `checksum` | `checksum` |
| `downloadStatus` | `download_status` |
| `thumbnailPath` | `thumbnail_path` |
| `createdAt` | `created_at` |

### CustomPlatform → custom_platforms

| Swift 字段名 | SQLite 列名 |
|-------------|------------|
| `id` | `id` |
| `name` | `name` |
| `logoPath` | `logo_path` |
| `createdAt` | `created_at` |
| `sortOrder` | `sort_order` |

### ImportTask → import_tasks

| Swift 字段名 | SQLite 列名 |
|-------------|------------|
| `id` | `id` |
| `originalURL` | `original_url` |
| `normalizedURL` | `normalized_url` |
| `platform` | `platform` |
| `status` | `status` |
| `progress` | `progress` |
| `errorMessage` | `error_message` |
| `itemID` | `item_id` |
| `createdAt` | `created_at` |
| `completedAt` | `completed_at` |
| `updatedAt` | `updated_at` |
| `retryCount` | `retry_count` |

### TrashRecord → trash_records

| Swift 字段名 | SQLite 列名 |
|-------------|------------|
| `id` | `id` |
| `itemID` | `item_id` |
| `deletedAt` | `deleted_at` |
| `autoDeleteAt` | `auto_delete_at` |
| `originalFolderID` | `original_folder_id` |
| `originalArchiveStatus` | `original_archive_status` |
| `mediaPaths` | `media_paths` |

---

## 3. Swift 类型 ↔ 跨平台 JSON 类型映射

| Swift 类型 | JSON Schema 类型 | SQLite 类型 | 存储格式 |
|-----------|-----------------|------------|---------|
| `UUID` | `string` (format: uuid) | `TEXT` | UUID 字符串（无连字符或带连字符均可） |
| `String` | `string` | `TEXT` | UTF-8 |
| `String?` | `string \| null` | `TEXT` | NULL 表示无值 |
| `Int` | `integer` | `INTEGER` | 直接存储 |
| `Int64` | `integer` | `INTEGER` | 直接存储 |
| `Double` | `number` | `REAL` | 直接存储 |
| `Bool` | `boolean` | `INTEGER` | 0 = false, 1 = true |
| `Date` | `string` (format: date-time) | `REAL` | Unix timestamp（秒） |
| `Date?` | `string \| null` | `REAL` | NULL 表示无值 |
| `[String]` | `array` of `string` | `TEXT` | JSON 序列化字符串 |
| `Platform` | `string` (enum) | `TEXT` | rawValue 字符串 |
| 其他 enum | `string` (enum) | `TEXT` | rawValue 字符串 |

---

## 4. 时间字段规范

### 存储格式

- **Swift**: `Date` 类型
- **SQLite**: `REAL` 类型，存储 `timeIntervalSince1970`（Unix timestamp，单位：秒）
- **JSON**: ISO 8601 字符串（如 `2025-01-15T10:30:00Z`）
- **C#/.NET**: `DateTime` 或 `DateTimeOffset`，需做 `DateTimeOffset.ToUnixTimeSeconds()` ↔ `DateTimeOffset.FromUnixTimeSeconds()` 转换

### 时间字段清单

| 模型 | 字段 | 说明 |
|------|------|------|
| Item | `importDate` | 导入时间，NOT NULL |
| Item | `modifyDate` | 修改时间，NOT NULL |
| Item | `publishDate` | 发布时间，可为 NULL |
| Item | `deletedAt` | 删除时间，NULL = 未删除 |
| Folder | `createdAt` | 创建时间，NOT NULL |
| MediaAsset | `createdAt` | 创建时间，NOT NULL |
| CustomPlatform | `createdAt` | 创建时间，NOT NULL |
| ImportTask | `createdAt` | 创建时间，NOT NULL |
| ImportTask | `completedAt` | 完成时间，可为 NULL |
| ImportTask | `updatedAt` | 最后更新时间，可为 NULL。用于 stale task 检测（10 分钟窗口） |
| TrashRecord | `deletedAt` | 删除时间，NOT NULL |
| TrashRecord | `autoDeleteAt` | 自动删除时间，NOT NULL |

---

## 5. enum rawValue 不可变规则

**所有 enum rawValue 是数据库存储值，不可随意更改。**

更改任何一个 rawValue 会导致：
- 已有数据库记录无法正确映射
- 跨平台数据不兼容
- 导入导出格式破坏

### 必须保持不变的 rawValue

详见 `enums.json`。以下为完整列表：

**Platform**: `douyin`, `xiaohongshu`, `coolapk`, `bilibili`, `github`, `youtube`, `x`, `weibo`, `zhihu`, `douban`, `custom`

**ArchiveStatus**: `favorite`, `inspiration`, `pending`, `archived`

**ContentStatus**: `normal`, `parseFailed`, `mediaIncomplete`, `sourceDeleted`, `trashed`

**DownloadStatus**: `pending`, `downloading`, `completed`, `failed`, `skipped`

**MediaStatus**: `complete`, `partial`, `failed`, `textOnly`

**MediaType**: `image`, `cover`, `video`, `thumbnail`

**TaskStatus**: `pending`, `recognizing`, `parsing`, `downloading`, `completed`, `failed`

---

## 6. Windows 端实现规则

### C# Model 必须遵守的规则

1. **字段名使用 C# 命名规范**（PascalCase），但数据库交互时必须按"字段名映射表"转换为 snake_case
2. **enum rawValue 必须与 Swift 完全一致**（字符串比较，大小写敏感）
3. **UUID 存储为 TEXT**，C# 端使用 `Guid`，序列化为带连字符的字符串（`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`）
4. **Bool 存储为 INTEGER**（0/1），C# 端需要做 `bool ↔ int` 转换
5. **Date 存储为 REAL**（Unix timestamp 秒），C# 端使用 `DateTimeOffset` 转换
6. **`[String]` (mediaPaths) 存储为 JSON TEXT**，C# 端使用 `JsonSerializer.Serialize/Deserialize`
7. **不要发明新字段**，不要删除已有字段
8. **不要改变字段含义**

### 推荐 C# 类型对应

| Swift 类型 | C# 类型 | SQLite 处理 |
|-----------|---------|------------|
| `UUID` | `Guid` | `ToString()` ↔ `Guid.Parse()` |
| `String` | `string` | 直接读写 |
| `Int` | `int` | 直接读写 |
| `Int64` | `long` | 直接读写 |
| `Double` | `double` | 直接读写 |
| `Bool` | `bool` | `Convert.ToInt32()` ↔ `Convert.ToBoolean()` |
| `Date` | `DateTimeOffset` | `ToUnixTimeSeconds()` ↔ `FromUnixTimeSeconds()` |
| `[String]` | `List<string>` | `JsonSerializer.Serialize()` ↔ `JsonSerializer.Deserialize()` |
| `Platform` | `enum` (string-backed) | rawValue 字符串 |

---

## 7. 导入导出字段规范

### 导入时必须使用的字段

| 用途 | 字段 | 说明 |
|------|------|------|
| 去重 | `normalizedURL` | 标准化后的 URL |
| 去重 | `platformContentID` + `platform` | 平台内容 ID + 平台 |
| 内容存储 | `title`, `body`, `author`, `authorID`, `publishDate` | 解析器输出 |
| 媒体 | `coverAssetID` → `MediaAsset` | 封面图 |
| 媒体 | `MediaAsset` 列表 | 所有图片/视频 |

### 导出时必须包含的字段

- 所有数据库字段（完整备份）
- 媒体文件（`media/` 目录）
- 平台 Logo（`platform_logos/` 目录）
- 数据库文件（`archiver.db`）

---

## 8. 不可随意改名的字段

以下字段名直接影响数据库兼容性，不可更改：

- 所有 SQLite 列名（snake_case）
- 所有 enum rawValue
- 表名（`items`, `folders`, `media_assets`, `custom_platforms`, `import_tasks`, `trash_records`）
- FTS5 虚拟表名（`items_fts`）
- 索引名（`idx_*`）

Swift 字段名（camelCase）可以在各平台独立命名，但建议保持一致以减少映射成本。

---

## 9. 运行时模型（非持久化）

### ParsedContent

`ParsedContent` 是解析器的输出数据结构，**不存储到数据库**。它作为解析结果传递给 `ImportService`，由 ImportService 将其转换为 `Item` + `MediaAsset` 记录。

| 字段 | 类型 | 说明 |
|------|------|------|
| `title` | `String?` | 标题 |
| `body` | `String?` | 正文 |
| `author` | `String?` | 作者 |
| `authorID` | `String?` | 平台作者 ID |
| `publishDate` | `Date?` | 发布时间 |
| `coverURL` | `String?` | 封面图 URL |
| `imageURLs` | `[String]` | 图片 URL 列表 |
| `videoURL` | `String?` | 视频 URL |
| `platformContentID` | `String?` | 平台内容 ID |
| `rawMetadata` | `[String: String]` | 原始元数据（调试用） |

Windows 端解析器应输出等价的数据结构，然后由导入服务转换为持久化模型。
