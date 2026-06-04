# shared/import-export — 跨平台导入/导出格式契约

本目录定义拾屿 Archiver 的备份导出和还原导入格式规范。

## 目录结构

```
shared/import-export/
  README.md                      — 本文件
  import-export-contract.md      — 完整导入/导出契约文档
  manifest.schema.json           — manifest.json 的字段定义（未来格式）
  backup-package.schema.json     — 导出包目录结构定义
  item-export.schema.json        — 导出时 Item 字段定义
  folder-export.schema.json      — 导出时 Folder 字段定义
  media-export.schema.json       — 导出时 MediaAsset 字段定义
  samples/                       — 示例文件（预留）
```

## 关键约束

- 导出包内禁止使用绝对路径
- 媒体文件使用相对路径 `{itemUUID}/fileName`
- 时间字段使用 Unix timestamp（秒）
- UUID 使用 string + uuid format
- 冲突处理默认策略为 skip
- zip 文件名建议使用 ASCII（避免 Windows 编码问题）
