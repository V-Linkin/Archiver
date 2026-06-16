# macOS 平台改名、内容归类与新内容导入逻辑审计

> 审计分支: feature/phase-7e-platform-management
> 审计 HEAD: a28df1d
> 审计时间: 2026-06-14
> 审计阶段: Stage 1 + Stage 2 完整审计

---

## A. 工具目录说明

```
tools/dbquery/ — 本地诊断工具，用于查询真实数据库
非本次审计创建，未提交，不在审计范围内
```

---

## B. 系统平台（YouTube/B站/GitHub）改名调用链

### 1. 存储位置

**文件**: `Utilities/PlatformCustomization.swift` line 4-43

```swift
struct PlatformCustomization {
    private static let prefix = "platform_custom_"
    
    static func displayName(for platform: Platform) -> String {
        guard platform != .custom else { return platform.rawValue }
        let key = "\(prefix)name_\(platform.rawValue)"
        return UserDefaults.standard.string(forKey: key) ?? platform.defaultDisplayName
    }
    
    static func setDisplayName(_ name: String, for platform: Platform) {
        let key = "\(prefix)name_\(platform.rawValue)"
        UserDefaults.standard.set(name, forKey: key)
    }
}
```

### 2. 默认显示名称

**文件**: `Models/Enums/Platform.swift` line 19-33

```swift
var defaultDisplayName: String {
    switch self {
    case .youtube: return "YouTube"
    case .bilibili: return "B站"
    case .github: return "GitHub"
    // ...
    }
}
```

### 3. displayName 计算属性

**文件**: `Models/Enums/Platform.swift` line 35-37

```swift
var displayName: String {
    PlatformCustomization.displayName(for: self)
}
```

### 4. 调用链

```
用户在平台页点击编辑
→ PlatformCustomization.setDisplayName(newName, for: platform)
→ UserDefaults.set(newName, forKey: "platform_custom_name_<rawValue>")
→ UI 通过 platform.displayName 读取 → UserDefaults lookup
```

### 5. 关键结论

- UserDefaults key 格式: `platform_custom_name_youtube`
- Platform.rawValue 永远不变（youtube/bilibili/github 固定）
- 改名只改 UserDefaults，不影响 custom_platforms 表
- 连续改名: 第二次改名覆盖同一 key，读取时返回最新值

---

## C. CustomPlatform（用户自建平台）改名调用链

### 1. 编辑入口

**文件**: `Views/Platform/EditCustomPlatformSheet.swift` line 3-43

```swift
struct EditCustomPlatformSheet: View {
    let platform: CustomPlatform
    // ...
    Button("保存") {
        var updated = platform
        updated.name = platformName.trimmingCharacters(in: .whitespaces)
        try? appState.customPlatformRepo.update(updated)
        appState.refreshData()
        dismiss()
    }
}
```

### 2. Repository.update

**文件**: `Database/CustomPlatformRepository.swift` line 41-52

```swift
func update(_ platform: CustomPlatform) throws {
    try db.write { db in
        try db.execute(sql: """
            UPDATE custom_platforms SET name=?, logo_path=?, sort_order=? WHERE id=?
        """, arguments: [
            platform.name,
            platform.logoPath,
            platform.sortOrder,
            platform.id.uuidString
        ])
    }
}
```

### 3. 调用链

```
用户在管理页面点击编辑
→ EditCustomPlatformSheet
→ Repository.update(updated)
→ SQL: UPDATE custom_platforms SET name=? WHERE id=?
→ appState.refreshData()
→ Sidebar + 首页重新加载
```

### 4. 关键结论

- 只更新 name/logoPath/sortOrder，不修改 items 表
- platform.id 不变
- 不调用 findMatchingCustomPlatform 或任何二次绑定
- 改名后，已有 custom_platform_id 指向该平台的 item 保持归属

---

## D. URL 识别与 Parser 路由

### 1. URL 识别

**文件**: `Utilities/URLNormalizer.swift` line 29-64

```swift
static func recognizePlatform(_ urlString: String) -> Platform? {
    let lower = urlString.lowercased()
    if lower.contains("youtube.com") || lower.contains("youtu.be") { return .youtube }
    if lower.contains("bilibili.com") || lower.contains("b23.tv") { return .bilibili }
    if lower.contains("github.com") { return .github }
    // ... 其他平台
}
```

返回值: Platform enum (.youtube/.bilibili/.github 等)

### 2. Parser 路由

**文件**: `Parsers/PlatformRouter.swift` line 10-23

```swift
private init() {
    parsers = [
        BilibiliParser(),
        GitHubParser(),
        YouTubeParser(),
        // ... 其他 parser
    ]
}

func parser(for url: URL) -> ContentParser? {
    parsers.first { $0.canParse(url: url) }
}
```

### 3. ParsedContent

**文件**: `Parsers/PlatformRouter.swift` line 36-51

```swift
func parse(urlString: String) async throws -> (ParsedContent, ContentParser) {
    // ...
    let content = try await parser.parse(url: url)
    return (content, parser)
}
```

**关键发现**: ParsedContent 不包含 platform 字段，也不包含 customPlatformID。Parser 只负责解析内容。

---

## E. 导入完整调用链

### 1. 导入入口

**文件**: `Services/ImportService.swift` line 36-175

```swift
func importURL(_ urlString: String) async -> ImportResult {
    // 1. 识别平台
    guard let detectedPlatform = URLNormalizer.recognizePlatform(urlString) else { ... }
    
    // 2. 去重检查
    let normalizedURL = URLNormalizer.normalize(urlString, platform: detectedPlatform)
    if let existingItem = try? itemRepo.findByNormalizedURL(normalizedURL) { ... }
    
    // 3. 查找匹配的自定义平台 ← 关键步骤
    let customPlatformID = findMatchingCustomPlatform(for: detectedPlatform)
    
    // 4. 创建 Item
    let item = createItem(
        from: parsedContent, url: urlString,
        platform: detectedPlatform, normalizedURL: normalizedURL,
        customPlatformID: customPlatformID
    )
    
    // 5. 保存
    try itemRepo.insert(item)
}
```

### 2. findMatchingCustomPlatform

**文件**: `Services/ImportService.swift` line 180-192

```swift
private func findMatchingCustomPlatform(for platform: Platform) -> UUID? {
    let allPlatforms = (try? customPlatformRepo.fetchAll()) ?? []
    let targetName = platform.defaultDisplayName
    let match = allPlatforms.first { $0.name.caseInsensitiveCompare(targetName) == .orderedSame }
    return match?.id
}
```

**核心逻辑**:
- 取 `platform.defaultDisplayName` (如 "YouTube"/"B站"/"GitHub")
- 在所有 CustomPlatform 中查找 name 与 defaultDisplayName 大小写不敏感匹配的记录
- 如果找到，返回该 CustomPlatform 的 UUID
- 如果找不到，返回 nil

### 3. Item 构造

**文件**: `Services/ImportService.swift` line 194-212

```swift
private func createItem(from content: ParsedContent, url: String,
                        platform: Platform, normalizedURL: String,
                        customPlatformID: UUID?) -> Item {
    let item = Item(
        // ...
        platform: customPlatformID != nil ? .custom : platform,
        // ...
        customPlatformID: customPlatformID
    )
    return item
}
```

**关键逻辑**:
- 如果 findMatchingCustomPlatform 返回了 UUID: `Item.platform = .custom`, `Item.customPlatformID = 匹配的 UUID`
- 如果返回 nil: `Item.platform = detectedPlatform` (如 .youtube), `Item.customPlatformID = nil`

---

## F. 平台创建时的自动归类

### 1. 创建入口

**文件**: `Views/Platform/NewCustomPlatformSheet.swift` line 82-115

```swift
private func createPlatform() {
    let name = platformName.trimmingCharacters(in: .whitespaces)
    var cp = CustomPlatform(name: name)
    try? appState.customPlatformRepo.insert(cp)
    
    // 自动归类已有内容
    let matchedCount = autoAssignUncategorized(name: name, platformID: cp.id)
    appState.refreshData()
}
```

### 2. autoAssignUncategorized

**文件**: `Views/Platform/NewCustomPlatformSheet.swift` line 117-136

```swift
private func autoAssignUncategorized(name: String, platformID: UUID) -> Int {
    guard let matchedPlatform = Platform.allCases.first(where: {
        $0.defaultDisplayName.caseInsensitiveCompare(name) == .orderedSame
    }) else { return 0 }
    
    let allItems = (try? appState.itemRepo.fetchAll()) ?? []
    let uncategorized = allItems.filter { $0.customPlatformID == nil }
    
    var count = 0
    for item in uncategorized {
        if URLNormalizer.recognizePlatform(item.originalURL) == matchedPlatform {
            var updated = item
            updated.customPlatformID = platformID
            updated.platform = .custom
            try? appState.itemRepo.update(updated)
            count += 1
        }
    }
    return count
}
```

**核心逻辑**:
- 用户创建名为 "YouTube" 的 CustomPlatform
- autoAssignUncategorized 检查: "YouTube" 是否与某个 Platform.defaultDisplayName 匹配
- 如果匹配 (youtube.defaultDisplayName == "YouTube"):
  - 遍历所有 customPlatformID==nil 的 item
  - 对每个 item 调用 URLNormalizer.recognizePlatform(item.originalURL)
  - 如果 URL 识别为 .youtube，将 item.customPlatformID 设置为新平台 ID，item.platform 设为 .custom
- 如果不匹配 (如用户创建名为 "工作" 的平台): 返回 0，不执行自动归类

---

## G. Sidebar 调用链

### 1. Sidebar 数据加载

**文件**: `App/ArchiverApp.swift` line 73-98

```swift
func refreshData() {
    Task.detached { [weak self] in
        guard let self else { return }
        let customPlatforms = (try? self.customPlatformRepo.fetchAll()) ?? []
        let recentItems = (try? self.itemRepo.fetchRecent()) ?? []
        
        var counts: [UUID: Int] = [:]
        for cp in customPlatforms {
            let count = (try? self.itemRepo.countByCustomPlatform(cp.id)) ?? 0
            counts[cp.id] = count
        }
        
        await MainActor.run {
            self.customPlatforms = customPlatforms
            self.customPlatformCounts = counts
        }
    }
}
```

### 2. CustomPlatform 入口

**文件**: `Views/Home/HomeView.swift` line 250-258

```swift
ForEach(appState.customPlatforms) { cp in
    Button {
        selectedNav = .customPlatform(cp.id)
    } label: {
        CustomPlatformCard(platform: cp, count: appState.customPlatformCounts[cp.id] ?? 0)
    }
}
```

### 3. CustomPlatformCard 显示名

**文件**: `Views/Home/HomeView.swift` line 289

```swift
Text(platform.name)  // 直接使用 CustomPlatform.name
```

### 4. 关键结论

- Sidebar 的 CustomPlatform 入口来自 `CustomPlatformRepository.fetchAll()`
- 显示名称直接使用 `CustomPlatform.name`（数据库字段）
- count 通过 `ItemRepository.countByCustomPlatform(UUID)` 查询: `WHERE custom_platform_id=? AND deleted_at IS NULL`
- 零内容 CustomPlatform 也会显示（count=0）
- 系统平台 (YouTube/B站) 的入口来自 `Platform.allCases`，显示名使用 `platform.displayName`（UserDefaults）

---

## H. 平台页查询

### 1. 系统平台页 (PlatformView)

**文件**: `Views/Platform/PlatformView.swift` line 254-269

```swift
private func loadData() {
    let loadedItems = (try? itemRepo.fetchAll(platform: platform)) ?? []
}
```

**文件**: `Database/ItemRepository.swift` line 139-164

```swift
func fetchAll(platform: Platform? = nil, ...) throws -> [Item] {
    var sql = "SELECT * FROM items WHERE deleted_at IS NULL"
    if let platform = platform {
        sql += " AND platform=?"
        args.append(platform.rawValue)
    }
}
```

**查询条件**: `WHERE platform='youtube' AND deleted_at IS NULL`

### 2. CustomPlatform 页 (CustomPlatformContentView)

**文件**: `Views/Platform/CustomPlatformContentView.swift` line 264-280

```swift
private func loadData() {
    customPlatform = try? appState.customPlatformRepo.find(id: customPlatformID)
    let loadedItems = (try? itemRepo.fetchByCustomPlatformID(customPlatformID)) ?? []
}
```

**文件**: `Database/ItemRepository.swift` line 217-224

```swift
func fetchByCustomPlatformID(_ platformID: UUID) throws -> [Item] {
    try db.read { db in
        try fetchAll(db,
            sql: "SELECT * FROM items WHERE custom_platform_id=? AND deleted_at IS NULL ORDER BY import_date DESC",
            arguments: [platformID.uuidString]
        )
    }
}
```

**查询条件**: `WHERE custom_platform_id='...' AND deleted_at IS NULL`

### 3. 关键结论

- 系统平台页: 只按 `items.platform` 查询（如 WHERE platform='youtube'）
- CustomPlatform 页: 只按 `items.custom_platform_id` 查询
- 两套查询完全独立，不存在 OR 合并
- CustomPlatform.name 改名不影响查询条件（查询只用 ID）
- 系统平台 displayName 改名不影响查询条件（查询只用 rawValue）

---

## I. 未分类查询

### 1. 查询代码

**文件**: `Database/ItemRepository.swift` line 226-235

```swift
func fetchUncategorizedItems() throws -> [Item] {
    try db.read { db in
        try fetchAll(db,
            sql: "SELECT * FROM items WHERE platform=? AND custom_platform_id IS NULL AND deleted_at IS NULL ORDER BY import_date DESC",
            arguments: [Platform.custom.rawValue]
        )
    }
}
```

### 2. 调用者

**文件**: `Views/Platform/UncategorizedContentView.swift` line 173-188

```swift
private func loadData() {
    let loadedItems = (try? itemRepo.fetchUncategorizedItems()) ?? []
}
```

### 3. 关键结论

- 未分类 = `WHERE platform='custom' AND custom_platform_id IS NULL AND deleted_at IS NULL`
- youtube/bilibili/github 内容永远不会进入未分类（因为它们的 platform 不是 'custom'）
- 只有在导入时被匹配到 CustomPlatform 但后来 CustomPlatform 被删除的 item 才会进入未分类
- CustomPlatform 改名不会使已有 item 进入未分类（因为 custom_platform_id 不变）

---

## J. 三个具体场景

### 场景 1: YouTube

```
初始: CustomPlatform.name = "YouTube" (platformID = P1)
已有 item: platform=.custom, customPlatformID=P1, originalURL="youtube.com/..."

改名: CustomPlatform.name → "you" (仍通过 Repository.update 只改 name)

新导入 YouTube URL:
1. URLNormalizer.recognizePlatform → .youtube
2. findMatchingCustomPlatform(.youtube):
   - targetName = "YouTube" (platform.defaultDisplayName)
   - 遍历 CustomPlatform: name="you" → "you".caseInsensitiveCompare("YouTube") != .orderedSame
   - 返回 nil
3. createItem: Item.platform = .youtube, Item.customPlatformID = nil
4. 新 item: platform=.youtube, customPlatformID=nil

结果:
- 已有旧 item: 仍在 CustomPlatform P1 (customPlatformID=P1)
- 新 item: 在系统 YouTube 平台 (platform=.youtube)
- Sidebar: "you" 平台 count = 1 (旧 item)，系统 YouTube 平台 count = 1 (新 item)
- 未分类: 不包含新旧 item
```

### 场景 2: B站

```
初始: CustomPlatform.name = "B站" (platformID = P2)
已有 item: platform=.custom, customPlatformID=P2

改名: CustomPlatform.name → "哔哩"

新导入 Bilibili URL:
1. URLNormalizer.recognizePlatform → .bilibili
2. findMatchingCustomPlatform(.bilibili):
   - targetName = "B站"
   - name="哔哩" → 不匹配
   - 返回 nil
3. Item.platform = .bilibili, Item.customPlatformID = nil

结果: 与 YouTube 场景相同
```

### 场景 3: GitHub

```
初始: CustomPlatform.name = "GitHub" (platformID = P3)
已有 item: platform=.custom, customPlatformID=P3

改名: CustomPlatform.name → "代码仓库"

新导入 GitHub URL:
1. URLNormalizer.recognizePlatform → .github
2. findMatchingCustomPlatform(.github):
   - targetName = "GitHub"
   - name="代码仓库" → 不匹配
   - 返回 nil
3. Item.platform = .github, Item.customPlatformID = nil

结果: 与 YouTube 场景相同
```

### 场景 4: 用户自建平台（非系统名称）

```
创建: CustomPlatform.name = "工作" (platformID = P4)

autoAssignUncategorized:
- Platform.allCases 中没有 defaultDisplayName == "工作" 的平台
- 返回 0，不执行自动归类

新导入 URL (任意平台):
- findMatchingCustomPlatform: 无匹配
- Item.platform = 检测到的平台, Item.customPlatformID = nil

结论: 用户自建非系统名称平台，不会自动接收任何导入内容。
必须通过手动"移动到平台"操作来归类内容。
```

---

## K. 隐式稳定关联排查

| 项目 | 状态 | 证据 |
|---|---|---|
| 固定 UUID | 不存在 | CustomPlatform.id 在创建时生成随机 UUID |
| 默认 CustomPlatform ID | 不存在 | 无预置 seed 数据 |
| Platform → CustomPlatform ID 映射 | 不存在 | 无内存映射或持久化映射 |
| 名称别名字典 | 不存在 | 无别名表或字典 |
| 首次启动 seed | 不存在 | 无 bootstrap 代码 |
| 备份恢复映射 | 不适用 | 备份恢复保持原 ID 不变 |
| Logo 映射 | 不存在 | Logo 仅用于显示 |
| sortOrder 映射 | 不存在 | sortOrder 只影响排序 |

**唯一稳定关联机制**: `ImportService.findMatchingCustomPlatform` 在导入时通过 `CustomPlatform.name.caseInsensitiveCompare(platform.defaultDisplayName)` 进行匹配。这是一个**临时匹配**，不是持久化映射。

---

## L. macOS 测试覆盖

### 搜索结果

```
未发现与 CustomPlatform 改名、动态归类、导入匹配相关的自动化测试。
```

macOS 项目有测试目录但不覆盖平台管理相关逻辑。

---

## M. macOS 真实产品语义结论

### 1. 两套独立的平台显示名称系统

| 类型 | 存储位置 | 读取方式 | 改名方式 |
|---|---|---|---|
| 系统平台 (YouTube/B站) | UserDefaults | `platform.displayName` | `PlatformCustomization.setDisplayName` |
| CustomPlatform (用户自建) | SQLite | `customPlatform.name` | `Repository.update` |

### 2. 内容归属规则

| 条件 | 归属 |
|---|---|
| `Item.customPlatformID = P` (有效) | 属于 CustomPlatform P |
| `Item.customPlatformID = nil` 且 `Item.platform = .youtube` | 属于系统 YouTube 平台 |
| `Item.customPlatformID = nil` 且 `Item.platform = .bilibili` | 属于系统 B站 平台 |
| `Item.customPlatformID = nil` 且 `Item.platform = .github` | 属于系统 GitHub 平台 |
| `Item.platform = .custom` 且 `Item.customPlatformID = nil` | 未分类 |

### 3. 导入时的匹配逻辑

```
URL → Platform enum (youtube/bilibili/github/...)
→ findMatchingCustomPlatform:
   CustomPlatform.name.caseInsensitiveCompare(platform.defaultDisplayName)
→ 如果匹配: Item.platform = .custom, Item.customPlatformID = 匹配的 UUID
→ 如果不匹配: Item.platform = Platform enum, Item.customPlatformID = nil
```

### 4. 改名后的行为

| 操作 | 系统平台 | CustomPlatform |
|---|---|---|
| 改名方式 | UserDefaults | 数据库 name |
| 已有内容 | 不变 (platform 不变) | 不变 (customPlatformID 不变) |
| 新导入内容 | 仍进入系统平台 (platform 不变) | 如果 name 不再匹配 defaultDisplayName，新内容进入系统平台 |
| 平台页查询 | WHERE platform=rawValue | WHERE custom_platform_id=id |
| Sidebar 显示名 | UserDefaults | CustomPlatform.name |

### 5. macOS 没有 canonicalKey 动态匹配机制

此前假设的 `canonicalKey(name)` 函数**不存在于 macOS 代码中**。

macOS 的"名称匹配"仅发生在两个位置:
1. `ImportService.findMatchingCustomPlatform` — 导入时匹配 `CustomPlatform.name` 与 `Platform.defaultDisplayName`
2. `NewCustomPlatformSheet.autoAssignUncategorized` — 创建平台时匹配 `Platform.defaultDisplayName` 与用户输入名称

两者都使用 `caseInsensitiveCompare`，且都只在导入/创建时执行一次，不是持续动态匹配。

---

## N. 审计验收表

| 项目 | 状态 | 证据位置 |
|---|---|---|
| 系统平台改名调用链 | 完成 | PlatformCustomization.swift line 7-16 |
| CustomPlatform改名调用链 | 完成 | EditCustomPlatformSheet.swift line 28-34, CustomPlatformRepository.swift line 41-52 |
| URL与Parser路由 | 完成 | URLNormalizer.swift line 29-64, PlatformRouter.swift line 10-51 |
| Item构造和platform赋值 | 完成 | ImportService.swift line 194-212 |
| customPlatformID赋值 | 完成 | ImportService.swift line 68, 180-192, 209 |
| 系统Sidebar入口 | 完成 | ArchiverApp.swift line 77-89, HomeView.swift line 250-258 |
| CustomPlatform Sidebar入口 | 完成 | ArchiverApp.swift line 77-89, HomeView.swift line 289 |
| 系统平台页查询 | 完成 | PlatformView.swift line 254-259, ItemRepository.swift line 139-164 |
| CustomPlatform页查询 | 完成 | CustomPlatformContentView.swift line 264-270, ItemRepository.swift line 217-224 |
| 未分类真实语义 | 完成 | ItemRepository.swift line 226-235 |
| YouTube场景 | 完成 | Section J 场景 1 |
| B站场景 | 完成 | Section J 场景 2 |
| GitHub场景 | 完成 | Section J 场景 3 |
| 用户自建平台场景 | 完成 | Section J 场景 4 |
| 隐式稳定关联 | 完成 | Section K |
| macOS测试覆盖 | 完成 | Section L（无覆盖） |

---

## O. Stage 1 已确认事实（仅限模型和 schema）

1. CustomPlatform 字段: id (UUID), name (String), logoPath (String?), createdAt (Date), sortOrder (Int)
2. 无 platform_key, display_name, type, source, metadata 字段
3. name 无 UNIQUE 约束
4. Item.platform: Platform enum (Parser 检测的来源)
5. Item.customPlatformID: UUID? (用户分配的平台，可空)
6. Platform enum rawValue 固定不变
7. custom_platforms 表无映射/别名/seed
8. Repository.update 只改 name/logoPath/sortOrder，不改 items
9. System platforms 使用 UserDefaults 存储 display name

---

## P. Stage 2 已确认事实（含调用链证据）

1. macOS 没有 canonicalKey 函数
2. 导入时通过 `findMatchingCustomPlatform` 匹配 CustomPlatform.name 与 Platform.defaultDisplayName
3. 匹配结果: Item.platform = .custom (匹配) 或 Platform enum (不匹配)
4. 匹配结果: Item.customPlatformID = UUID (匹配) 或 nil (不匹配)
5. 未分类 = WHERE platform='custom' AND custom_platform_id IS NULL
6. 系统平台 page: WHERE platform=rawValue
7. CustomPlatform page: WHERE custom_platform_id=id
8. Sidebar count: WHERE custom_platform_id=id (CustomPlatform) 或 WHERE platform=rawValue (系统平台)
9. 改名后已有内容通过 custom_platform_id 保持归属（不受影响）
10. 改名后新导入内容如果 name 不再匹配 defaultDisplayName，进入系统平台而非 CustomPlatform
