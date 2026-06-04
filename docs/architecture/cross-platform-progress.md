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

结论：

* 原本写在 `DatabaseManager.swift` 中的 SQLite schema 和 FTS5 SQL 已抽成独立 `.sql` 文件
* GRDB migration 名称保持不变：

  * `v1_createTables`
  * `v2_fts`

* 现有数据库升级路径不变
* 新库创建正常
* 旧库兼容正常
* FTS5 rebuild 和搜索正常
* `custom_platforms` 仍由 `CustomPlatformRepository.setupTable()` 创建
* `schema.sql` 和 `fts5.sql` 是跨平台契约文档，不参与运行时 migration

重要限制：

* 不改变表名
* 不改变字段名
* 不改变索引名
* 不改变 FTS5 表名
* 不改变 migration 名称
* 不改变已有用户数据库升级路径

---

### Phase 2A：Platform.swift 去 SwiftUI

状态：已完成并验收通过。

改动：

```text
Models/Enums/Platform.swift
Models/Enums/Platform+UI.swift
```

结论：

* `Platform.swift` 已移除 `import SwiftUI`
* `Platform.swift` 现在只保留平台无关数据逻辑
* `iconName` 和 `brandColor` 已移动到 `Platform+UI.swift`
* 现有调用方式不变：

  * `platform.iconName`
  * `platform.brandColor`

* 所有 enum case 和 rawValue 不变
* 数据库存储值不变

重要限制：

* 不改变任何平台 rawValue
* 不改变 `Platform.custom`
* 不把 SwiftUI、Color、SF Symbols 写回核心枚举
* UI 展示属性必须留在 macOS UI 扩展中

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

结论：

* 只新增文档和 JSON Schema
* 未修改 Swift 源码
* 未修改数据库
* 所有 Swift Model 与 SQLite schema 一致
* `enums.json` 记录跨平台 enum rawValue 和默认显示名
* JSON Schema 使用跨平台模型字段语义
* `model-contract.md` 记录 Swift 字段与 SQLite 字段映射关系

重要规则：

* UUID：JSON 中为 string + uuid format，数据库中为 TEXT
* Date：数据库中为 REAL，格式为 Unix timestamp seconds
* Bool：JSON 中为 boolean，数据库中为 INTEGER 0/1
* Enum：数据库中为 rawValue TEXT
* `mediaPaths`：模型层是 string array，数据库中是 JSON 序列化 TEXT
* 不包含 SwiftUI Color
* 不包含 SF Symbols iconName

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

当前 macOS 备份格式：

```text
Archiver备份_YYYYMMDD_HHmm.zip
  archiver.db
  media/
  platform_logos/
  backup_info.json
```

未来推荐跨平台导出格式：

```text
archiver-export-YYYYMMDD-HHmm.zip
  manifest.json
  database/
    archiver.sqlite
  media/
  custom-platforms/
    logos/
```

结论：

* 当前备份 zip 内部路径均为相对路径
* 当前 `media_assets.local_path` 为 `{itemUUID}/fileName`，跨平台友好
* 当前元信息文件是 `backup_info.json`
* 未来推荐使用 `manifest.json`
* 本阶段未修改现有 BackupService / ImportService
* 本阶段只定义契约，不实现新格式

重要规则：

* zip 内部路径必须使用 `/`
* 不允许保存 `/Users/...`
* 不允许保存 `C:\Users\...`
* 不允许保存 App 沙盒绝对路径
* 当前中文备份文件名可保留
* 未来跨平台建议使用 ASCII 文件名：

  * `archiver-export-YYYYMMDD-HHmm.zip`

---

## 3. 当前进行中阶段

### Phase 3A：URLNormalizer 契约

状态：分析完成，待执行。

目标：

```text
shared/url/
  README.md
  url-normalizer-contract.md
  url-normalizer-rules.json
  url-normalizer-test-cases.json
```

URLNormalizer 公开方法：

| 方法 | 输入 | 输出 | 用途 |
|------|------|------|------|
| `extractURLs(from:)` | String (混合文本) | [String] | 提取所有支持平台的 URL |
| `extractFirstURL(from:)` | String | String? | 提取第一个 URL |
| `recognizePlatform(_:)` | String (URL) | Platform? | 识别平台 |
| `normalize(_:platform:)` | String + Platform | String | 标准化 URL（去重） |
| `extractContentID(_:platform:)` | String + Platform | String? | 提取内容 ID |
| `isValidURL(_:)` | String | Bool | 验证 URL 合法性 |

支持的平台（10 个）：

| 平台 | 域名 | normalizedURL 格式 |
|------|------|-------------------|
| douyin | douyin.com, iesdouyin.com | douyin://video/{id} |
| xiaohongshu | xiaohongshu.com, xhslink.com | xiaohongshu://explore/{id} |
| coolapk | coolapk.com, coolapk1s.com | coolapk://feed/{id} |
| bilibili | bilibili.com, b23.tv | bilibili://video/{id} |
| github | github.com | github://repo/{owner}/{repo} |
| youtube | youtube.com, youtu.be | youtube://video/{id} |
| x | x.com, twitter.com | x://tweet/{id} |
| weibo | weibo.com, m.weibo.cn | weibo://status/{id} |
| zhihu | zhihu.com | zhihu://content/{id} |
| douban | douban.com | douban://subject/{id} |

核心特征：

* 纯 Foundation 依赖，无 macOS / SwiftUI / AppKit 依赖
* 所有正则均为标准正则表达式，跨语言可复用
* `normalizedURL` 影响数据库去重
* `platformContentID` 影响导入重复判断
* 未显式清理 tracking 参数（`utm_*`、`spm` 等），因为自定义 scheme URL 只含 ID

重要限制：

* 不修改 `URLNormalizer.swift`
* 不修改 `PlatformRouter.swift`
* 不修改 Parser
* 不创建 Windows 项目
* 不改变去重规则
* 不改变 normalizedURL 规则
* 不改变 platformContentID 规则

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

禁止跳过只读分析。

禁止一次性做大范围重构。

禁止在一个 Phase 中混入其它 Phase 的工作。

---

## 5. 每轮任务默认限制

除非用户明确允许，否则每轮任务默认遵守：

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

每个 Phase 或子 Phase 验收通过后，必须单独提交。

推荐提交信息：

```text
Phase 1:
Phase 1: 抽离 shared/db 跨平台数据库层

Phase 2A:
Phase 2A: 拆分 Platform.swift，移除 SwiftUI 依赖

Phase 2B:
Phase 2B: 创建 shared/model 跨平台模型契约

Phase 2C:
Phase 2C: 创建跨平台导入/导出格式契约

Phase 3A:
Phase 3A: 创建 URLNormalizer 跨平台规则契约
```

每次进入下一阶段前，工作区应保持干净：

```bash
git status
```

应显示：

```text
nothing to commit, working tree clean
```

---

## 7. 构建验证要求

每个阶段完成后，默认执行：

```bash
xcodegen generate
xcodebuild build -project Archiver.xcodeproj -scheme Archiver -destination 'platform=macOS'
```

预期：

```text
BUILD SUCCEEDED
```

如果当前阶段只新增文档或 JSON Schema，仍应执行构建验证，确保 XcodeGen/project 配置未被破坏。

---

## 8. 后续阶段计划

### Phase 3A：URLNormalizer 契约

创建：

```text
shared/url/
```

只记录现有 URL 识别、标准化、content ID 提取规则。

不修改 `URLNormalizer.swift`。

---

### Phase 3B：Parser Contract

创建：

```text
shared/parsers/
  parser-contract.md
  platform-parser-rules.json
```

目标：

* 记录 10 个平台解析器的输入输出
* 记录 HTTP 优先级
* 记录 WebView 降级策略
* 区分可跨平台规则和平台专属实现

不重写 Parser。

---

### Phase 3C：Parser Fixtures

创建：

```text
shared/parsers/fixtures/
```

目标：

* 每个平台至少 3-5 个测试样例
* 包括输入 URL、期望平台、期望字段
* Windows 端未来用同一批 fixtures 验证解析一致性

---

### Phase 4：macOS 内部边界优化

目标：

* View 不直接操作数据库
* ViewModel 调用 Service
* Repository 只处理数据访问
* Service 处理业务流程

此阶段开始才允许小范围重构 Swift 代码。

---

### Phase 5：Windows MVP

推荐技术栈：

```text
Avalonia + C# + SQLite
```

最低 MVP：

```text
打开同一个 SQLite 数据库
列表页
搜索
详情页
导入/导出
基础设置
```

Windows 端应遵守：

```text
shared/db
shared/model
shared/import-export
shared/url
shared/parsers
```

---

## 9. 当前重点风险

### 数据库风险

不得改变：

```text
表名
字段名
索引名
FTS5 表名
migration 名称
enum rawValue
```

### 去重风险

不得随意改变：

```text
normalizedURL
platformContentID
ImportService duplicate 判断
SearchRepository fallback 行为
```

### 解析风险

Parser 中有部分平台依赖 WebView：

```text
Weibo
Zhihu
Coolapk
Douban
Xiaohongshu
```

其中 Douban 风险最高，因为可能依赖 WebView 通过挑战页。

### 备份风险

当前备份格式和未来推荐格式不同。
文档中必须明确区分：

```text
当前已实现格式
未来推荐格式
兼容迁移建议
```

---

## 10. 给未来 AI 的固定提示

以后每次继续跨平台迁移任务时，先执行：

```text
请先读取 docs/architecture/cross-platform-progress.md，并严格遵守其中的执行规范。

本轮任务只做：[填写具体 Phase]

请先只读分析相关文件，输出影响范围和建议。
在我确认前，不要修改代码。
```
