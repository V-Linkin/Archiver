# 拾屿 Archiver 跨平台迁移进度与执行规范

## 1. 项目背景

当前项目是一个完全本地运行的 macOS 桌面应用，无服务器、无云后端。

当前技术栈：

```text
Swift 6.0
SwiftUI
GRDB 7
SQLite + FTS5
macOS 14.0+
XcodeGen: xcodegen generate → .xcodeproj
```

目标是未来开发 Windows 版本，但不推翻现有 macOS 版本。

长期架构方向：

```text
macOS:
  SwiftUI + GRDB + SQLite

Windows:
  Avalonia + C# + SQLite
  或 Tauri + Web UI + SQLite

shared:
  数据库契约
  数据模型契约
  导入导出契约
  URL 标准化规则
  Parser 规则
  搜索规则
  测试用例
```

核心原则：

```text
不是共享服务器后端
而是共享本地数据协议、业务规则、导入导出格式、测试标准
```

---

## 2. 当前已完成阶段

### Phase 1：shared/db 抽离

状态：已完成并验收通过。

新增：

```text
shared/db/
  README.md
  schema.sql
  fts5.sql
  migrations/
    v1_create_tables.sql
    v2_fts.sql
```

改动：

```text
Database/DatabaseManager.swift
project.yml
```

---

### Phase 2A：Platform.swift 去 SwiftUI

状态：已完成并验收通过。

改动：

```text
Models/Enums/Platform.swift
Models/Enums/Platform+UI.swift
```

---

### Phase 2B：shared/model 契约

状态：已完成并验收通过。

新增：

```text
shared/model/
  README.md
  enums.json
  item.schema.json
  folder.schema.json
  media_asset.schema.json
  custom_platform.schema.json
  import_task.schema.json
  trash_record.schema.json
  model-contract.md
```

---

### Phase 2C：shared/import-export 契约

状态：已完成并验收通过。

新增：

```text
shared/import-export/
  README.md
  import-export-contract.md
  manifest.schema.json
  backup-package.schema.json
  item-export.schema.json
  folder-export.schema.json
  media-export.schema.json
  samples/
    README.md
```

---

### Phase 3A：URLNormalizer 契约

状态：已完成并验收通过。

新增：

```text
shared/url/
  README.md
  url-normalizer-contract.md
  url-normalizer-rules.json
  url-normalizer-test-cases.json
```

---

### Phase 3B：Parser Contract

状态：已完成并验收通过。

新增：

```text
shared/parsers/
  README.md
  parser-contract.md
  platform-parser-rules.json
```

结论：

* 只新增文档和 JSON 规则，未修改 Swift 源码
* 未修改数据库
* 10 个平台 Parser 的解析策略、依赖、输出字段已完整记录
* 平台风险分级已完成：
  - 低风险：Bilibili、GitHub
  - 中风险：Douyin、YouTube、X、Weibo
  - 高风险：Xiaohongshu、Coolapk、Zhihu、Douban
* WebView 依赖关系已明确记录
* 短链展开边界已明确记录
* 第三方 API 依赖已记录（fxtwitter、api.zhihu.com、coolapk1s.com）
* JSWebLoader 作为 macOS 专属实现已记录，Windows 需用 WebView2 重新实现

重要规则：

* Parser 输出 ParsedContent 是运行时模型，不是数据库持久化模型
* rawMetadata 是平台专属扩展字段，不可作为跨平台核心依赖
* Windows 端必须通过 Phase 3C 的 fixtures 测试
* URLNormalizer 不做网络请求，短链展开属于 Parser 层

---

## 3. 当前进行中阶段

### Phase 3C：Parser Fixtures

状态：待开始。

目标：

```text
shared/parsers/fixtures/
```

每个平台至少 3-5 个测试样例，包括输入 URL、期望平台、期望字段。

---

## 4. 总体执行原则

以后每个 AI 编程任务必须遵守以下流程：

```text
1. 先读取本文件：docs/architecture/cross-platform-progress.md
2. 读取当前任务相关源码和 shared 契约文件
3. 先做只读分析
4. 输出分析结果
5. 等用户确认
6. 再执行最小改动
7. 构建验证
8. 输出变更报告
9. 用户确认后 Git commit
```

---

## 5. 每轮任务默认限制

```text
不移动现有 macOS 目录
不创建 Windows 项目
不修改数据库 schema
不改变 enum rawValue
不改变数据库字段名
不改变 normalizedURL 规则
不改变 platformContentID 规则
不改变导入去重逻辑
不改变现有 App 运行行为
不顺手优化无关代码
不重命名已有文件
不删除已有文件
不把未来推荐格式伪装成当前已实现格式
```

---

## 6. Git 提交规则

```text
Phase 1: Phase 1: 抽离 shared/db 跨平台数据库层
Phase 2A: Phase 2A: 拆分 Platform.swift，移除 SwiftUI 依赖
Phase 2B: Phase 2B: 创建 shared/model 跨平台模型契约
Phase 2C: Phase 2C: 创建跨平台导入/导出格式契约
Phase 3A: Phase 3A: 创建 URLNormalizer 跨平台规则契约
Phase 3B: Phase 3B: 创建 Parser 跨平台契约
```

---

## 7. 构建验证要求

```bash
xcodegen generate
xcodebuild build -project Archiver.xcodeproj -scheme Archiver -destination 'platform=macOS'
```

---

## 8. 后续阶段计划

### Phase 3C：Parser Fixtures

每个平台至少 3-5 个测试样例，Windows 端未来用同一批 fixtures 验证解析一致性。

---

### Phase 4：macOS 内部边界优化

View 不直接操作数据库，ViewModel 调用 Service，Repository 只处理数据访问。

---

### Phase 5：Windows MVP

推荐技术栈 Avalonia + C# + SQLite，最低 MVP 包括列表页、搜索、详情页、导入/导出。

---

## 9. 当前重点风险

### 数据库风险
不得改变：表名、字段名、索引名、FTS5 表名、migration 名称、enum rawValue

### 去重风险
不得随意改变：normalizedURL、platformContentID、ImportService duplicate 判断

### 解析风险
Parser 中有部分平台依赖 WebView：Xiaohongshu、Coolapk、Zhihu、Douban
其中 Douban 风险最高：2 秒限频 + JS 依赖 DOM + 反爬

### 备份风险
当前备份格式和未来推荐格式不同，必须明确区分

---

## 10. 给未来 AI 的固定提示

```text
请先读取 docs/architecture/cross-platform-progress.md，并严格遵守其中的执行规范。

本轮任务只做：[填写具体 Phase]

请先只读分析相关文件，输出影响范围和建议。
在我确认前，不要修改代码。
```
