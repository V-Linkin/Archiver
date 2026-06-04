# shared/db — 跨平台数据库层

本目录包含拾屿 Archiver 的 SQLite 数据库 schema 定义，供 macOS 和 Windows 共享。

## 目录结构

```
shared/db/
  README.md                    — 本文件
  schema.sql                   — 完整 schema 汇总（契约文档，非运行时直接执行）
  fts5.sql                     — FTS5 全文搜索契约文档
  migrations/
    v1_create_tables.sql       — 建表 + 索引（5 张表，10 个索引）
    v2_fts.sql                 — FTS5 虚拟表
```

## 运行时使用

macOS 端（Swift/GRDB）：`Database/DatabaseManager.swift` 在 GRDB migration 回调中读取本目录的 `.sql` 文件执行。

Windows 端（未来）：直接读取 `migrations/` 下的 `.sql` 文件执行，或参考 `schema.sql` 建库。

## 表清单

| 表名 | 说明 | 来源 |
|------|------|------|
| `items` | 内容主体 | v1_create_tables.sql |
| `media_assets` | 媒体资产 | v1_create_tables.sql |
| `folders` | 文件夹 | v1_create_tables.sql |
| `trash_records` | 回收站记录 | v1_create_tables.sql |
| `import_tasks` | 导入任务 | v1_create_tables.sql |
| `custom_platforms` | 自定义平台 | CustomPlatformRepository（未纳入 GRDB migration） |
| `items_fts` | FTS5 全文搜索虚拟表 | v2_fts.sql |

## Migration 名称

| GRDB Migration 名称 | SQL 文件 | 说明 |
|---------------------|----------|------|
| `v1_createTables` | `migrations/v1_create_tables.sql` | 建表 + 索引 |
| `v2_fts` | `migrations/v2_fts.sql` | FTS5 虚拟表 |

## 注意事项

- `custom_platforms` 表由 `CustomPlatformRepository.setupTable()` 在运行时独立创建，不在 DatabaseManager 的 GRDB migration 体系内。
- 两个 `ALTER TABLE ADD COLUMN custom_platform_id` 容错逻辑保留在 `DatabaseManager.swift` 中，用于兼容旧版数据库。
- 不要修改任何表名、字段名、索引名或 FTS5 配置。
