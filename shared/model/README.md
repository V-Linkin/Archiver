# shared/model — 跨平台模型契约

本目录包含拾屿 Archiver 的数据模型契约定义，供 macOS 和 Windows 共享。

## 目录结构

```
shared/model/
  README.md                    — 本文件
  model-contract.md            — 完整模型契约文档（映射规则、类型对应、实现约束）
  enums.json                   — 所有跨平台枚举定义（rawValue + 默认显示名）
  item.schema.json             — Item 模型 JSON Schema
  folder.schema.json           — Folder 模型 JSON Schema
  media_asset.schema.json      — MediaAsset 模型 JSON Schema
  custom_platform.schema.json  — CustomPlatform 模型 JSON Schema
  import_task.schema.json      — ImportTask 模型 JSON Schema
  trash_record.schema.json     — TrashRecord 模型 JSON Schema
```

## 使用方式

- **macOS 端**: Swift Model 位于 `Models/` 目录，与本契约保持一致
- **Windows 端（未来）**: 按 `model-contract.md` 中的规则实现 C# Model
- **跨平台验证**: 对比 `enums.json` 和各 `.schema.json` 确保一致性

## 关键约束

- enum rawValue 不可更改（影响数据库兼容）
- SQLite 列名不可更改（影响数据库兼容）
- 不发明新字段，不删除已有字段
- 时间字段统一使用 Unix timestamp（秒）
- Bool 字段在 SQLite 中存储为 INTEGER 0/1
- UUID 字段在 SQLite 中存储为 TEXT
