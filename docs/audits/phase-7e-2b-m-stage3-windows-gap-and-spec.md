# Phase 7E-2B-M Stage 3: Windows Gap Analysis & Cross-Platform Spec

> 审计分支: feature/phase-7e-platform-management
> 审计 HEAD: a28df1d
> 审计时间: 2026-06-14
> Stage 2 文档: docs/audits/phase-7e-2b-m-stage2-macos-flow-audit.md

---

## A. 仓库状态

```
分支: feature/phase-7e-platform-management
HEAD: a28df1d
```

未提交文件:
```
M  windows/src/Gatherly.Windows/ViewModels/MainWindowViewModel.cs          — Phase 7E-2B
M  windows/src/Gatherly.Windows/ViewModels/PlatformManagementViewModel.cs  — Phase 7E-2B
M  windows/src/Gatherly.Windows/Views/PlatformManagementWindow.axaml       — Phase 7E-2B
M  windows/src/Gatherly.Windows/Views/PlatformManagementWindow.axaml.cs    — Phase 7E-2B
M  windows/src/Gatherly.Windows/Services/HomeDataService.cs               — Phase 7E-2B
M  windows/src/Gatherly.Windows/Database/ItemRepository.cs                — Phase 7E-2B
```

未跟踪目录:
```
?? tools/          — 诊断工具
?? docs/audits/    — 审计文档
```

测试文件: 无未提交测试文件修改。

---

## B. macOS 关键事实引用（来自 Stage 2 文档）

### B1. macOS Sidebar 只有 CustomPlatform 入口

**文件**: `App/ContentView.swift` line 226-279 (SidebarView)

```swift
Section("平台") {
    ForEach(appState.customPlatforms) { cp in          // ← 只有 customPlatforms
        NavigationLink(value: NavigationTarget.customPlatform(cp.id)) {
            Label { Text(cp.name) } icon: { ... }
        }
    }
    NavigationLink(value: NavigationTarget.uncategorized) {
        Label { Text("未分类内容") } icon: { ... }
    }
    Button { appState.showNewCustomPlatform = true } label: {
        Label("新增平台", systemImage: "plus.circle")
    }
}
```

**macOS Sidebar 没有系统平台入口**。YouTube/B站/GitHub 等系统平台不在侧边栏显示。用户只能通过 CustomPlatform 名称匹配来间接关联。

### B2. macOS 系统平台页面可直接导航但不在 Sidebar

**文件**: `App/ContentView.swift` line 171-172

```swift
case .platform(let p):
    PlatformView(platform: p, selectedNav: $selectedNav, previousNav: $previousNav)
```

`NavigationTarget.platform(Platform)` 存在，但 SidebarView 没有提供此导航入口。

### B3. macOS 导入匹配

**文件**: `Services/ImportService.swift` line 180-192

```swift
private func findMatchingCustomPlatform(for platform: Platform) -> UUID? {
    let allPlatforms = (try? customPlatformRepo.fetchAll()) ?? []
    let targetName = platform.defaultDisplayName
    let match = allPlatforms.first { $0.name.caseInsensitiveCompare(targetName) == .orderedSame }
    return match?.id
}
```

### B4. macOS 创建时自动归类

**文件**: `Views/Platform/NewCustomPlatformSheet.swift` line 117-136

```swift
private func autoAssignUncategorized(name: String, platformID: UUID) -> Int {
    guard let matchedPlatform = Platform.allCases.first(where: {
        $0.defaultDisplayName.caseInsensitiveCompare(name) == .orderedSame
    }) else { return 0 }
    // ... 遍历 items, URLNormalizer.recognizePlatform, 设置 customPlatformID
}
```

### B5. macOS 未分类

**文件**: `Database/ItemRepository.swift` line 226-235

```swift
func fetchUncategorizedItems() throws -> [Item] {
    // WHERE platform=? AND custom_platform_id IS NULL AND deleted_at IS NULL
    // arguments: [Platform.custom.rawValue]  ← 只有 platform='custom'
}
```

### B6. macOS 平台页查询

- 系统平台: `WHERE platform=rawValue` (PlatformView line 259)
- CustomPlatform: `WHERE custom_platform_id=id` (CustomPlatformContentView line 270)
- 两套查询完全独立

---

## C. Windows 当前实现审计

### C1. Windows ImportService — 无 findMatchingCustomPlatform

**文件**: `windows/src/Gatherly.Windows/Services/ImportService.cs` line 151-177

```csharp
var item = new Item
{
    Id = Guid.NewGuid(),
    Title = content.Title,
    Body = content.Body,
    OriginalUrl = content.OriginalUrl ?? url,
    Platform = platform.Value,                    // ← 直接使用检测到的 Platform enum
    PlatformContentId = content.PlatformContentId ?? contentId,
    NormalizedUrl = content.NormalizedUrl ?? normalizedUrl,
    Author = content.Author,
    AuthorId = content.AuthorId,
    PublishDate = content.PublishDate,
    ImportDate = DateTimeOffset.UtcNow,
    ModifyDate = DateTimeOffset.UtcNow,
    ContentStatus = ContentStatus.normal,
    ArchiveStatus = ArchiveStatus.pending,
    MediaStatus = ...,
    CoverUrl = content.CoverUrl
};
await _itemRepo.InsertAsync(item);
```

**结论**:
1. `item.Platform = platform.Value` — 直接赋值检测到的 Platform enum
2. `item.CustomPlatformId` — **从未赋值**，保持默认 null
3. ImportService 构造函数只接收 `_itemRepo`, `_taskRepo`, `_mediaDownload`, `_router`, `_timeProvider`
4. **不引用 CustomPlatformRepository**
5. **没有 findMatchingCustomPlatform 方法**
6. **没有 autoAssignUncategorized 方法**
7. 匹配成功/失败的概念在 Windows 导入中**不存在**

### C2. Windows 未分类 SQL

**文件**: `windows/src/Gatherly.Windows/Database/ItemRepository.cs` line 130-142

```csharp
public async Task<List<Item>> GetUncategorizedItemsAsync()
{
    cmd.CommandText = @"SELECT * FROM items 
        WHERE deleted_at IS NULL 
          AND custom_platform_id IS NULL 
          AND lower(platform) NOT IN ('youtube', 'bilibili', 'github')
        ORDER BY import_date DESC";
}
```

**文件**: `windows/src/Gatherly.Windows/Services/HomeDataService.cs` line 277-286

```csharp
private async Task<int> GetUncategorizedItemCountAsync()
{
    cmd.CommandText = @"SELECT COUNT(*) FROM items 
        WHERE custom_platform_id IS NULL 
          AND deleted_at IS NULL
          AND lower(platform) NOT IN ('youtube', 'bilibili', 'github')";
}
```

**macOS 未分类**: `WHERE platform='custom' AND custom_platform_id IS NULL AND deleted_at IS NULL`
**Windows 未分类**: `WHERE custom_platform_id IS NULL AND deleted_at IS NULL AND lower(platform) NOT IN ('youtube', 'bilibili', 'github')`

**关键差异**: Windows 排除 youtube/bilibili/github 但不检查 platform='custom'。macOS 只检查 platform='custom'。

### C3. Windows Sidebar — 合并双入口

**文件**: `windows/src/Gatherly.Windows/Services/HomeDataService.cs` line 125-232

```csharp
public async Task<List<PlatformEntryDisplay>> GetPlatformStatsAsync()
{
    var customPlatforms = await _customPlatformRepo.GetAllAsync();

    // 建立 canonicalKey → custom platform ids 映射
    var canonicalToCustomIds = new Dictionary<string, List<Guid>>();
    foreach (var cp in customPlatforms)
    {
        var key = GetCanonicalKey(cp.Name);
        canonicalToCustomIds[key].Add(cp.Id);
    }

    // 遍历标准平台，合并对应 custom_platforms
    foreach (var p in standardPlatforms)
    {
        var customIds = canonicalToCustomIds.GetValueOrDefault(canonicalKey);
        totalCount = await GetPlatformItemCountAsync(p, customIds);
        // 只要 customIds.Count > 0 或 SupportsMergedPlatform，就显示
        result.Add(new PlatformEntryDisplay
        {
            Name = p.GetDisplayName(),
            StandardPlatform = p,
            CustomPlatformIds = customIds   // ← 合并！
        });
    }

    // 添加没有匹配标准平台的 custom_platforms
    foreach (var cp in customPlatforms)
    {
        if (processedCanonicalKeys.Contains(key)) continue;  // ← 已合并的不重复
        result.Add(new PlatformEntryDisplay { Id = cp.Id, Name = cp.Name });
    }
}
```

**关键问题**:
1. `GetCanonicalKey(cp.Name)` 使用 `PlatformAliases` 字典匹配 CustomPlatform.name
2. 如果 CustomPlatform.name="YouTube"，canonicalKey="youtube"，与系统 YouTube 合并
3. 如果 CustomPlatform.name="you"，canonicalKey="you"，不匹配任何别名，作为独立入口显示
4. **count 使用 OR 合并**: `WHERE platform=$platform OR custom_platform_id=$cpId`

**文件**: `windows/src/Gatherly.Windows/Services/HomeDataService.cs` line 116-119

```csharp
private static bool SupportsMergedPlatform(Platform platform)
{
    return platform == Platform.youtube || platform == Platform.bilibili;
}
```

### C4. Windows CustomPlatform 删除

**文件**: `windows/src/Gatherly.Windows/Database/CustomPlatformRepository.cs` line 126-174

```csharp
public async Task<DeletePlatformResult> DeleteAsync(Guid id)
{
    var transaction = _connection.BeginTransaction();
    // 1. UPDATE items SET custom_platform_id=NULL WHERE custom_platform_id=$id
    // 2. DELETE FROM custom_platforms WHERE id=$id
    transaction.Commit();
}
```

**结论**: 删除平台时只将 custom_platform_id 设为 NULL，不修改 platform 字段。
- macOS 删除时: `item.customPlatformID = nil; item.platform = .custom`
- Windows 删除时: `custom_platform_id=NULL`（platform 字段保持不变）

**孤儿 custom_platform_id 查询**:
```sql
SELECT COUNT(*) FROM items i
LEFT JOIN custom_platforms cp ON cp.id = i.custom_platform_id COLLATE NOCASE
WHERE i.custom_platform_id IS NOT NULL AND cp.id IS NULL;
```

### C5. Windows 系统平台显示名

**文件**: `windows/src/Gatherly.Windows/Services/HomeDataService.cs` line 183

```csharp
Name = p.GetDisplayName(),   // ← 固定字符串，不可用户自定义
```

Windows 没有类似 macOS `PlatformCustomization` 的 UserDefaults/Settings 偏好存储。`GetDisplayName()` 返回固定字符串。

### C6. Windows PlatformManagementViewModel — GetCanonicalKey

**文件**: `windows/src/Gatherly.Windows/ViewModels/PlatformManagementViewModel.cs` line 61-67

```csharp
private static string GetCanonicalKey(string name)
{
    var lower = name.ToLowerInvariant();
    if (lower == "youtube" || lower == "youtu.be") return "youtube";
    if (lower == "bilibili" || lower == "b站" || lower == "哔哩哔哩") return "bilibili";
    return lower;
}
```

**注意**: 这个方法只包含 youtube/bilibili 两个别名，与 HomeDataService 的 PlatformAliases 不一致。

---

## D. 四个真实故障根因

### 故障 1: B站只能看似重命名一次

**文件**: `PlatformManagementViewModel.cs` line 56-59, 144-154

```csharp
private static readonly HashSet<string> SystemMergedKeys = new(StringComparer.OrdinalIgnoreCase)
{
    "youtube", "bilibili"
};

private void BeginEdit(CustomPlatform? platform)
{
    var key = GetCanonicalKey(platform.Name);
    if (SystemMergedKeys.Contains(key))
    {
        ErrorMessage = "系统合并平台名称不可修改。";
        return;
    }
}
```

**触发步骤**:
1. macOS 备份导入创建了 name="B站" 的 CustomPlatform
2. 用户尝试编辑: `GetCanonicalKey("B站")` → "bilibili" → 在 SystemMergedKeys 中 → 拒绝编辑
3. 但如果用户绕过（旧代码可能没有此保护），修改 name="哔哩"
4. 第二次编辑: `GetCanonicalKey("哔哩")` → "哔哩" → 不在 SystemMergedKeys 中 → 允许编辑
5. 但 Sidebar 标题 `p.GetDisplayName()` 是固定字符串 "B站"，不随 name 变化

**根因**: Windows 没有独立系统平台显示名存储。Sidebar 标题来自固定字符串，不是 CustomPlatform.name。BeginEdit 基于 name 的 canonicalKey 保护只对"标准名称"有效。

**macOS 差异**: macOS 系统平台改名通过 UserDefaults (`PlatformCustomization.setDisplayName`)，与 CustomPlatform.name 完全独立。

### 故障 2: YouTube 改名后内容表现不一致

**文件**: `HomeDataService.cs` line 116-119, 162-192

**触发步骤**:
1. CustomPlatform name="YouTube" 与系统 YouTube 合并显示
2. 改名为 "you": canonicalKey="you"，不再匹配 "youtube"
3. Sidebar 中 "you" 成为独立入口，系统 YouTube 入口消失（如果没有其他 customIds 匹配）
4. items.platform=youtube 的内容仍在 `GetPlatformItemCountAsync(Platform.youtube, [])` 中被统计

**根因**: 合并逻辑基于 canonicalKey，改名后 canonicalKey 变化导致合并关系断裂。

### 故障 3: CustomPlatform 改名后新导入内容不匹配

**这是 macOS 的正确行为，不是 Windows bug。**

macOS 中:
- findMatchingCustomPlatform 使用 `platform.defaultDisplayName` 匹配
- CustomPlatform 改名后 name 不再等于 defaultDisplayName
- 新导入内容 Item.platform=检测到的标准 Platform，不写 customPlatformID
- 旧内容通过 custom_platform_id 保持归属

**结论**: 此行为在 macOS 和 Windows 应保持一致。问题在于 Windows 没有 findMatchingCustomPlatform，所以从未发生过匹配。

### 故障 4: 未分类行为不一致

**macOS 未分类**:
```sql
WHERE platform='custom' AND custom_platform_id IS NULL AND deleted_at IS NULL
```

**Windows 未分类**:
```sql
WHERE custom_platform_id IS NULL AND deleted_at IS NULL AND lower(platform) NOT IN ('youtube', 'bilibili', 'github')
```

**差异**:
1. macOS: 只包含 platform='custom' 的内容（只有被匹配到 CustomPlatform 后又删除的内容）
2. Windows: 包含所有 custom_platform_id=NULL 且非 youtube/bilibili/github 的内容
3. Windows 的 douyin/xiaohongshu/coolapk/x/weibo/zhihu/douban 内容都在"未分类"中
4. macOS 的这些内容在各自系统平台中（通过 `WHERE platform=rawValue` 查询）

**根因**: Windows 没有为所有标准平台提供 Sidebar 入口。只有 SupportsMergedPlatform（youtube/bilibili）或有 CustomPlatform 匹配的标准平台才显示。其他平台的 items 通过 NOT IN 硬编码排除后进入"未分类"。

---

## E. 完整差距矩阵

| 行为 | macOS | Windows 当前 | 一致 | 证据 |
|---|---|---|---|---|
| 系统平台稳定身份 | Platform.rawValue | Platform enum (相同) | ✅ | Platform.cs |
| 系统平台显示名存储 | UserDefaults key=`platform_custom_name_<rawValue>` | 固定字符串 GetDisplayName() | ❌ | macOS: PlatformCustomization.swift; Windows: HomeDataService.cs line 183 |
| 系统平台改名 | PlatformCustomization.setDisplayName → UserDefaults | BeginEdit 拒绝 (SystemMergedKeys) | ❌ | macOS: PlatformCustomization.swift; Windows: PlatformManagementViewModel.cs line 148-154 |
| CustomPlatform 稳定身份 | UUID id | UUID id | ✅ | 两端相同 |
| CustomPlatform 显示名 | custom_platforms.name | custom_platforms.name | ✅ | 两端相同 |
| CustomPlatform 改名 | Repository.update(name) | CustomPlatformRepository.UpdateAsync(name) | ✅ | macOS: CustomPlatformRepository.swift; Windows: CustomPlatformRepository.cs line 82-124 |
| 创建标准名称 CustomPlatform | autoAssignUncategorized (遍历 items + URLNormalizer) | 无自动归类 | ❌ | macOS: NewCustomPlatformSheet.swift line 117-136; Windows: ImportService.cs 无相关代码 |
| 新导入匹配 CustomPlatform | findMatchingCustomPlatform(defaultDisplayName) | 无匹配 | ❌ | macOS: ImportService.swift line 180-192; Windows: ImportService.cs line 155-175 |
| 匹配成功 platform 字段 | .custom | N/A | ❌ | macOS: ImportService.swift line 201 |
| 匹配成功 customPlatformID | UUID | N/A | ❌ | macOS: ImportService.swift line 209 |
| Sidebar 入口来源 | customPlatforms only (无系统平台入口) | 标准平台 + CustomPlatform 合并 | ❌ | macOS: ContentView.swift line 226-279; Windows: HomeDataService.cs line 125-232 |
| 系统平台页查询 | WHERE platform=rawValue | WHERE platform=rawValue | ✅ | macOS: ItemRepository.swift line 145-147; Windows: ItemRepository.cs line 70-77 |
| CustomPlatform 页查询 | WHERE custom_platform_id=id | WHERE custom_platform_id=id | ✅ | macOS: ItemRepository.swift line 220-222; Windows: ItemRepository.cs line 122-128 |
| 系统与 CustomPlatform 合并 | 不合并 | 合并 (canonicalKey) | ❌ | macOS: ContentView.swift; Windows: HomeDataService.cs line 132-192 |
| 未分类定义 | platform='custom' AND custom_platform_id IS NULL | custom_platform_id IS NULL AND NOT IN (youtube,bilibili,github) | ❌ | macOS: ItemRepository.swift line 226-235; Windows: ItemRepository.cs line 130-142 |
| 删除 CustomPlatform 后 items | customPlatformID=nil, platform=.custom | custom_platform_id=NULL (platform 不变) | ❌ | macOS: ContentView.swift line 121-128; Windows: CustomPlatformRepository.cs line 139-145 |
| 改名后已有内容 | 不变 (ID 保持) | 不变 (ID 保持) | ✅ | 两端相同 |

---

## F. 唯一推荐对齐方案（已确认）

### F1. 系统平台显示名偏好存储

**选定实现**: 使用 System.Text.Json 保存到 `%LOCALAPPDATA%/Gatherly/platform_display_names.json`

理由:
1. 不新增第三方 NuGet 依赖（项目无 CommunityToolkit.Storage）
2. 复用现有 `DatabasePaths.DataDirectory`（`%LOCALAPPDATA%/Gatherly/`）
3. System.Text.Json 是 .NET 8 内置，无需额外包
4. 不修改 SQLite schema

**新增文件**: `Services/SystemPlatformDisplayNames.cs`

```csharp
using System.Text.Json;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;

namespace Gatherly.Windows.Services;

/// <summary>
/// 系统平台显示名称持久化存储
/// 文件路径: %LOCALAPPDATA%/Gatherly/platform_display_names.json
/// </summary>
public class SystemPlatformDisplayNames
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private static string FilePath => Path.Combine(DatabasePaths.DataDirectory, "platform_display_names.json");

    private Dictionary<string, string> _names;

    public SystemPlatformDisplayNames()
    {
        _names = Load();
    }

    /// <summary>
    /// 获取系统平台显示名称，未设置时回退到默认名称
    /// </summary>
    public string GetDisplayName(Platform platform)
    {
        if (_names.TryGetValue(platform.ToRawValue(), out var name))
            return name;
        return platform.GetDefaultDisplayName();
    }

    /// <summary>
    /// 设置系统平台显示名称
    /// </summary>
    public void SetDisplayName(Platform platform, string displayName)
    {
        _names[platform.ToRawValue()] = displayName;
        Save();
    }

    /// <summary>
    /// 重置为默认名称
    /// </summary>
    public void ResetDisplayName(Platform platform)
    {
        _names.Remove(platform.ToRawValue());
        Save();
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new();
            }
        }
        catch { }
        return new();
    }

    private void Save()
    {
        DatabasePaths.EnsureDataDirectory();
        var json = JsonSerializer.Serialize(_names, JsonOptions);
        File.WriteAllText(FilePath, json);
    }
}
```

**JSON 文件结构** (`platform_display_names.json`):
```json
{"youtube":"我的视频","bilibili":"B站"}
```

**使用位置**:
- `HomeDataService.GetPlatformStatsAsync` — 系统平台入口显示名（仅内部使用）
- `PlatformView.navigationTitle` — 系统平台页面标题
- `ItemDetailView` — 详情页平台显示
- `PlatformManagementViewModel` — 管理窗口系统平台显示

### F2. 移除 SystemMergedKeys，统一编辑逻辑

- BeginEdit 不再拒绝任何平台
- SaveAsync 区分: 系统平台 → 写 SystemPlatformPreferences; CustomPlatform → 写 name

### F3. 导入时 findMatchingCustomPlatform

复刻 macOS:
- 遍历 CustomPlatformRepository.GetAllAsync()
- 比较 name 与 Platform.GetDefaultDisplayName()（大小写不敏感）
- 匹配: item.platform=custom, item.customPlatformID=UUID
- 不匹配: item.platform=Platform, item.customPlatformID=null

### F4. 创建时 autoAssignUncategorized

复刻 macOS:
- 检查 name 是否与某个 Platform.GetDefaultDisplayName() 匹配
- 匹配则遍历 customPlatformID=null 的 items，URL 识别后设置

### F5. 未分类

改为: `WHERE platform='custom' AND custom_platform_id IS NULL AND deleted_at IS NULL`

### F6. 删除 CustomPlatform 后 items 处理

改为 macOS 行为: `item.customPlatformID=null; item.platform=.custom`

---

## G. 产品决策（已确认）

### G1. Sidebar 最终决策：CustomPlatform-only Sidebar

**最终决策**: 采用 macOS 的 CustomPlatform-only Sidebar。

Sidebar 只显示:
1. CustomPlatform 入口（来自 custom_platforms 表）
2. 未分类入口
3. 新增平台入口

Sidebar 不单独显示系统 Platform 入口。

当存在名称为默认平台名称的 CustomPlatform 时:
- YouTube 内容进入名称为 "YouTube" 的 CustomPlatform（通过导入匹配）
- B站内容进入名称为 "B站" 的 CustomPlatform
- GitHub 内容进入名称为 "GitHub" 的 CustomPlatform
- Sidebar 最终只显示这个 CustomPlatform 入口

这与用户此前要求 "YouTube 和 B站只显示一个入口" 完全一致。

### G2. 系统平台页面决策：保留但不在 Sidebar 显示

系统平台页面和内部导航路由继续保留:
- PlatformView / 系统平台查询能力保留
- 用于内部跳转、详情页来源跳转或后续功能

但不在 Sidebar 中直接生成系统平台入口。

---

## H. 实施阶段拆分

### Phase 7E-2B-1: 系统平台显示名称偏好存储与读取 [可直接执行]
- 目标: 新增 SystemPlatformDisplayNames，读写系统平台显示名
- 修改文件: 新增 Services/SystemPlatformDisplayNames.cs
- 数据库: 不变
- 测试: 新增 5+ 测试（读写、默认值、持久化）
- 真机验证: 改名后读取正确、重启后保持
- 提交: 是（通过验证后）

### Phase 7E-2B-2: 平台管理编辑逻辑 [可直接执行]
- 目标: 
  - 系统平台写 SystemPlatformDisplayNames（不写 CustomPlatform）
  - CustomPlatform 写 custom_platforms.name
  - 移除 SystemMergedKeys 编辑保护
  - 所有平台均可编辑
- 修改文件: PlatformManagementViewModel.cs, PlatformManagementWindow.axaml
- 数据库: 不变
- 测试: 新增 10+ 测试（系统平台编辑、CustomPlatform 编辑、连续改名）
- 真机验证: 连续改名、所有平台可编辑、Sidebar 标题更新
- 提交: 是（通过验证后）

### Phase 7E-2B-3: 导入匹配和创建平台自动归类 [可直接执行]
- 目标: 
  - ImportService 新增 findMatchingCustomPlatform（匹配 defaultDisplayName）
  - 创建平台时 autoAssignUncategorized（遍历 items + URLNormalizer）
- 修改文件: ImportService.cs, CreatePlatformDialog
- 数据库: 不变
- 测试: 新增 15+ 测试（匹配成功/失败、创建时自动归类）
- 真机验证: 创建 YouTube 平台自动归类已有 YouTube 内容
- 提交: 是（通过验证后）

### Phase 7E-2B-4: 删除平台与未分类规则对齐 [可直接执行]
- 目标:
  - 删除 CustomPlatform 后: item.customPlatformID=null, item.platform=.custom
  - 未分类 SQL 改为: WHERE platform='custom' AND custom_platform_id IS NULL AND deleted_at IS NULL
  - 移除 NOT IN (youtube, bilibili, github) 硬编码
- 修改文件: CustomPlatformRepository.cs, ItemRepository.cs, HomeDataService.cs
- 数据库: 不变
- 测试: 新增 5+ 测试（删除后 items 状态、未分类 SQL）
- 真机验证: 删除平台后内容进入未分类
- 提交: 是（通过验证后）

### Phase 7E-2B-5: Sidebar 移除合并逻辑 [可直接执行]
- 目标:
  - Sidebar 只显示 CustomPlatform 入口（不再合并系统平台）
  - 移除 SupportsMergedPlatform
  - 移除 canonicalKey 合并
  - 移除 PlatformAliases
  - 移除 GetPlatformItemCountAsync 的 OR 合并
  - 平台页按 custom_platform_id 独立查询
  - 未分类 count 使用 macOS 规则
- 修改文件: HomeDataService.cs, MainWindowViewModel.cs, MainWindow.axaml
- 数据库: 不变
- 测试: 新增 10+ 测试（Sidebar 入口、count、平台页）
- 真机验证: Sidebar 只显示 CustomPlatform 和未分类
- 提交: 是（通过验证后）

**注意**: 每个阶段修复完成并通过真机验证后再 commit。不要提前 commit 整个 Phase 7E-2B。

---

## I. 验收表

| 项目 | 状态 | 证据 |
|---|---|---|
| Windows系统平台改名机制 | 完成 | C5: GetDisplayName() 固定字符串, 无偏好存储 |
| Windows CustomPlatform改名机制 | 完成 | C6: CustomPlatformRepository.UpdateAsync(name), 只改数据库 |
| Windows导入匹配 | 完成 | C1: ImportService.cs line 155-175, 无 findMatchingCustomPlatform, CustomPlatformId 从未赋值 |
| Windows Sidebar双入口/合并机制 | 完成 | C3: HomeDataService.cs line 125-232, canonicalKey 合并 |
| Windows平台页查询 | 完成 | C3: GetByPlatformWithCustomAsync (OR 合并), GetByCustomPlatformIdAsync (独立) |
| Windows未分类规则 | 完成 | C2: ItemRepository.cs line 130-142, NOT IN (youtube,bilibili,github) |
| 四个故障根因 | 完成 | Section D |
| 完整差距矩阵 | 完成 | Section E |
| 唯一推荐方案 | 完成 | Section F |
| 跨平台统一规格 | 完成 | Section G |
| 实施阶段拆分 | 完成 | Section H |
| Sidebar 产品决策 | 已确认 | G1: CustomPlatform-only Sidebar |
| 系统平台页面决策 | 已确认 | G2: 保留但不在 Sidebar 显示 |
| 显示名存储实现 | 已确认 | F1: System.Text.Json + DatabasePaths.DataDirectory |
