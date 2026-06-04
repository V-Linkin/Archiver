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

核心原则：共享本地数据协议、业务规则、导入导出格式、测试标准。

---

## 2. 当前已完成阶段

### Phase 1：shared/db 抽离 ✅

```text
shared/db/ — schema.sql, fts5.sql, migrations/v1_create_tables.sql, v2_fts.sql
```

### Phase 2A：Platform.swift 去 SwiftUI ✅

```text
Models/Enums/Platform.swift — 移除 import SwiftUI
Models/Enums/Platform+UI.swift — brandColor, iconName
```

### Phase 2B：shared/model 契约 ✅

```text
shared/model/ — 6 个 JSON Schema + enums.json + model-contract.md
```

### Phase 2C：shared/import-export 契约 ✅

```text
shared/import-export/ — 备份格式契约 + 5 个 JSON Schema
```

### Phase 3A：URLNormalizer 契约 ✅

```text
shared/url/ — url-normalizer-contract.md + rules.json + test-cases.json
```

### Phase 3B：Parser Contract ✅

```text
shared/parsers/ — parser-contract.md + platform-parser-rules.json
```

### Phase 3C：Parser Fixtures ✅

```text
shared/parsers/fixtures/
  README.md
  fixture-schema.json
  bilibili/fixtures.json    (3 条)
  github/fixtures.json      (3 条)
  youtube/fixtures.json     (3 条)
  x/fixtures.json           (3 条)
  douyin/fixtures.json      (3 条)
  weibo/fixtures.json       (3 条)
  xiaohongshu/fixtures.json (3 条)
  coolapk/fixtures.json     (2 条)
  zhihu/fixtures.json       (3 条)
  douban/fixtures.json      (3 条)
```

总计 29 条 fixture，覆盖全部 10 个平台。

结论：

* 只新增测试数据，未修改 Swift 源码
* 未修改数据库
* 强断言（platform、contentID、normalizedURL）跨平台必须一致
* 弱断言（title/body/author 非空）允许平台改版后失败
* 短链样例标记 `shortLinkExpansion: true`，contentID 在展开前不可预测
* 高风险平台标记 `stability: volatile`
* Phase 3C 只提供测试数据，不提供测试执行器

---

## 3. 当前进行中阶段

### Phase 4：macOS 内部边界优化

状态：待开始。

目标：

* View 不直接操作数据库
* ViewModel 调用 Service
* Repository 只处理数据访问
* Service 处理业务流程

此阶段开始才允许小范围重构 Swift 代码。

---

## 4. 总体执行原则

```text
1. 先读取本文件
2. 读取相关源码和 shared 契约
3. 先做只读分析，等用户确认
4. 再执行最小改动
5. 构建验证
6. 输出变更报告
7. 用户确认后 Git commit
```

---

## 5. 每轮任务默认限制

```text
不移动现有 macOS 目录
不创建 Windows 项目
不修改数据库 schema
不改变 enum rawValue
不改变 normalizedURL 规则
不改变 platformContentID 规则
不改变现有 App 运行行为
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
Phase 3C: Phase 3C: 创建 Parser Fixtures 测试数据
```

---

## 7. 构建验证要求

```bash
xcodegen generate
xcodebuild build -project Archiver.xcodeproj -scheme Archiver -destination 'platform=macOS'
```

---

## 8. 后续阶段计划

### Phase 4：macOS 内部边界优化
View → ViewModel → Service → Repository 分层。

### Phase 5：Windows MVP
Avalonia + C# + SQLite，遵守 shared/ 下所有契约。

---

## 9. 当前重点风险

### 数据库风险
不得改变：表名、字段名、索引名、FTS5 表名、migration 名称、enum rawValue

### 去重风险
不得随意改变：normalizedURL、platformContentID

### 解析风险
高风险平台：Xiaohongshu、Coolapk、Zhihu、Douban（WebView/DOM 依赖）

### 备份风险
当前格式和未来推荐格式不同，必须明确区分

---

## 10. 给未来 AI 的固定提示

```text
请先读取 docs/architecture/cross-platform-progress.md，并严格遵守其中的执行规范。

本轮任务只做：[填写具体 Phase]

请先只读分析相关文件，输出影响范围和建议。
在我确认前，不要修改代码。
```
