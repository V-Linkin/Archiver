# 集藏 Gatherly — Flutter 跨平台迁移设计

> **日期**: 2026-06-03（更新）
> **状态**: 设计完成，待后续执行
> **策略**: 渐进式分离（方案 A）
> **当前版本**: macOS 版 v1.1.9（Swift/SwiftUI）

---

## 一、项目概述

### 产品定位

集藏 Gatherly — 跨平台的私人内容岛

帮助用户把散落在各个平台的内容统一收集、保存和管理。复制链接，粘贴进去，自动识别平台、抓取内容、下载图片，归档到私人资料库。

**所有数据存储在本地，不上传云端，不登录账号，完全属于你。**

### 迁移目标

用 Flutter (Dart) 重写整个应用，同时支持 macOS 和 Windows：

1. macOS 使用 `macos_ui` 包，还原原生 Sidebar 风格
2. Windows 使用 `fluent_ui` 包，还原 Fluent Design 风格
3. 共享 Dart 业务逻辑，各平台只写平台特定的 UI Widget
4. 数据库使用 SQLite（sqflite 包），与现有 Swift 版本完全兼容
5. GitHub 仓库：V-Linkin/Gatherly

### 为什么选择 Flutter

| 对比项 | Tauri (Rust + Web) | Flutter (Dart) |
|--------|-------------------|----------------|
| macOS 原生感 | CSS 模拟，不够优雅 | `macos_ui` 真原生组件 |
| Windows 原生感 | CSS 模拟 | `fluent_ui` 真原生组件 |
| UI 开发效率 | HTML/CSS 写样式累 | Widget 组合很快 |
| 视频播放 | WebView 内嵌，体验差 | `video_player` 原生渲染 |
| 图片查看 | HTML img 标签 | `photo_view` 原生手势 |
| 构建体积 | 5-10MB | 20-30MB |
| 学习难度 | Rust 很难 | Dart 很简单 |
| 代码复用 | 前端需写两套 CSS | 一套 Dart 代码 |

---

## 二、技术架构

### 整体架构

```
┌──────────────────────────────────────────────────┐
│                 Flutter App                       │
├──────────────────────┬───────────────────────────┤
│   macOS              │   Windows                  │
│   macos_ui           │   fluent_ui                │
│   Sidebar + 工具栏    │   NavigationView + CommandBar │
├──────────────────────┴───────────────────────────┤
│              Dart 共享层                           │
│  ┌─────────┐ ┌──────────┐ ┌─────────────────┐   │
│  │ Parsers │ │ Database │ │ File Storage    │   │
│  │ (10个)  │ │ (SQLite) │ │ (媒体文件管理)  │   │
│  └─────────┘ └──────────┘ └─────────────────┘   │
│  ┌──────────────────┐ ┌─────────────────────┐   │
│  │ Import Service   │ │ Search (FTS5)       │   │
│  └──────────────────┘ └─────────────────────┘   │
│  ┌──────────────────┐ ┌─────────────────────┐   │
│  │ Backup Service   │ │ Update Checker      │   │
│  └──────────────────┘ └─────────────────────┘   │
│  ┌──────────────────┐ ┌─────────────────────┐   │
│  │ Media Exporter   │ │ Browser Detector    │   │
│  └──────────────────┘ └─────────────────────┘   │
└──────────────────────────────────────────────────┘
```

### 技术选型

| 组件 | 选型 | 包名 |
|------|------|------|
| UI 框架 | Flutter 3.x | flutter |
| macOS UI | macos_ui | `macos_ui: ^1.0.0` |
| Windows UI | fluent_ui | `fluent_ui: ^4.0.0` |
| 数据库 | sqflite + sqflite_common_ffi | `sqflite: ^2.0.0` |
| 全文搜索 | sqflite FTS5 | sqflite 内置支持 |
| HTTP 请求 | dio | `dio: ^5.0.0` |
| HTML 解析 | html | `html: ^0.15.0` |
| Markdown 渲染 | flutter_markdown | `flutter_markdown: ^0.6.0` |
| 图片查看 | photo_view | `photo_view: ^0.14.0` |
| 视频播放 | video_player | `video_player: ^2.0.0` |
| 文件选择 | file_picker | `file_picker: ^6.0.0` |
| 路径管理 | path_provider | `path_provider: ^2.0.0` |
| 状态管理 | riverpod | `flutter_riverpod: ^2.0.0` |
| 网页加载 | webview_flutter | `webview_flutter: ^4.0.0` |
| 序列化 | json_annotation | `json_annotation: ^4.0.0` |

---

## 三、模块映射（Swift → Dart）

### 3.1 数据模型

```
当前 Swift                          →  Dart
──────────────────────────────────────────────────
Models/Item.swift                   →  lib/models/item.dart
Models/Folder.swift                 →  lib/models/folder.dart
Models/MediaAsset.swift             →  lib/models/media_asset.dart
Models/CustomPlatform.swift         →  lib/models/custom_platform.dart
Models/TrashRecord.swift            →  lib/models/trash_record.dart
Models/ImportTask.swift             →  lib/models/import_task.dart
Models/Enums/Platform.swift         →  lib/models/enums/platform.dart
Models/Enums/ContentStatus.swift    →  lib/models/enums/content_status.dart
Models/Enums/ArchiveStatus.swift    →  lib/models/enums/archive_status.dart
Models/Enums/MediaStatus.swift      →  lib/models/enums/media_status.dart
Models/Enums/MediaType.swift        →  lib/models/enums/media_type.dart
Models/Enums/DownloadStatus.swift   →  lib/models/enums/download_status.dart
Models/Enums/TaskStatus.swift       →  lib/models/enums/task_status.dart
```

### 3.2 数据库层

```
当前 Swift                          →  Dart
──────────────────────────────────────────────────
Database/DatabaseManager.swift      →  lib/database/database_helper.dart
Database/ItemRepository.swift       →  lib/database/repositories/item_repository.dart
Database/FolderRepository.swift     →  lib/database/repositories/folder_repository.dart
Database/MediaRepository.swift      →  lib/database/repositories/media_repository.dart
Database/SearchRepository.swift     →  lib/database/repositories/search_repository.dart
Database/TrashRepository.swift      →  lib/database/repositories/trash_repository.dart
Database/CustomPlatformRepository   →  lib/database/repositories/platform_repository.dart
```

### 3.3 解析器

```
当前 Swift                          →  Dart
──────────────────────────────────────────────────
Parsers/ContentParser.swift         →  lib/parsers/content_parser.dart（abstract class）
Parsers/BaseParser.swift            →  lib/parsers/base_parser.dart（基础实现）
Parsers/PlatformRouter.swift        →  lib/parsers/platform_router.dart
Parsers/DouyinParser.swift          →  lib/parsers/douyin_parser.dart
Parsers/XiaohongshuParser.swift     →  lib/parsers/xiaohongshu_parser.dart
Parsers/CoolapkParser.swift         →  lib/parsers/coolapk_parser.dart
Parsers/BilibiliParser.swift        →  lib/parsers/bilibili_parser.dart
Parsers/GitHubParser.swift          →  lib/parsers/github_parser.dart
Parsers/YouTubeParser.swift         →  lib/parsers/youtube_parser.dart
Parsers/XParser.swift               →  lib/parsers/twitter_parser.dart
Parsers/WeiboParser.swift           →  lib/parsers/weibo_parser.dart
Parsers/ZhihuParser.swift           →  lib/parsers/zhihu_parser.dart
Parsers/DoubanParser.swift          →  lib/parsers/douban_parser.dart
```

### 3.4 服务层

```
当前 Swift                          →  Dart
──────────────────────────────────────────────────
Services/ImportService.swift        →  lib/services/import_service.dart
Services/BackupService.swift        →  lib/services/backup_service.dart
Services/UpdateChecker.swift        →  lib/services/update_checker.dart
```

### 3.5 工具层

```
当前 Swift                          →  Dart
──────────────────────────────────────────────────
Utilities/URLNormalizer.swift       →  lib/utils/url_normalizer.dart
Utilities/DataDirectory.swift       →  lib/utils/data_directory.dart
Utilities/BrowserDetector.swift     →  lib/utils/browser_detector.dart
Utilities/PlatformCustomization.swift → lib/utils/platform_customization.dart
Utilities/MediaExporter.swift       →  lib/utils/media_exporter.dart
Utilities/FilePicker.swift          →  lib/utils/file_picker_helper.dart
Utilities/JSWebLoader.swift         →  lib/utils/js_web_loader.dart
Utilities/ViewerWindowManager.swift →  lib/utils/viewer_window_manager.dart
```

### 3.6 视图层（macOS 用 macos_ui，Windows 用 fluent_ui）

```
当前 SwiftUI 视图                    →  Flutter Widget
──────────────────────────────────────────────────
Views/Home/HomeView.swift           →  lib/views/home/home_view.dart
Views/Platform/PlatformView.swift   →  lib/views/platform/platform_view.dart
Views/Platform/FolderView.swift     →  lib/views/platform/folder_view.dart
Views/Platform/CustomPlatformContentView.swift → lib/views/platform/custom_platform_view.dart
Views/Platform/UncategorizedContentView.swift  → lib/views/platform/uncategorized_view.dart
Views/Platform/NewCustomPlatformSheet.swift    → lib/views/platform/new_platform_sheet.dart
Views/Platform/EditCustomPlatformSheet.swift   → lib/views/platform/edit_platform_sheet.dart
Views/Platform/ChangeLogoSheet.swift           → lib/views/platform/change_logo_sheet.dart
Views/Item/ItemDetailView.swift     →  lib/views/item/item_detail_view.dart
Views/Item/EditItemView.swift       →  lib/views/item/edit_item_view.dart
Views/Item/NewItemView.swift        →  lib/views/item/new_item_view.dart
Views/Search/SearchResultsView.swift → lib/views/search/search_view.dart
Views/Trash/TrashView.swift         →  lib/views/trash/trash_view.dart
Views/Settings/SettingsView.swift   →  lib/views/settings/settings_view.dart
Views/Settings/HelpView.swift       →  lib/views/settings/help_view.dart
Views/Components/ItemCardView.swift →  lib/views/components/item_card.dart
Views/Components/MarkdownView.swift →  lib/views/components/markdown_viewer.dart
Views/Components/MarkdownEditor.swift → lib/views/components/markdown_editor.dart
Views/Components/ImageViewerView.swift → lib/views/components/image_viewer.dart
Views/Components/VideoPlayerView.swift → lib/views/components/video_player.dart
Views/Components/VideoViewerView.swift → lib/views/components/video_viewer.dart
Views/Components/VideoThumbnailView.swift → lib/views/components/video_thumbnail.dart
Views/Components/ToastView.swift    →  lib/views/components/toast.dart
Views/Components/ExportPickerSheet.swift → lib/views/components/export_picker.dart
Views/Components/PlaceholderTextEditor.swift → lib/views/components/placeholder_editor.dart
Views/Components/DebounceHelper.swift    → lib/utils/debounce.dart
```

---

## 四、数据库兼容性

### SQLite Schema 完全复用

Flutter 的 `sqflite` 直接读写同一份 SQLite 数据库文件，表结构不变。

```dart
// lib/database/database_helper.dart
import 'package:sqflite/sqflite.dart';
import 'package:path/path.dart';

class DatabaseHelper {
  static Database? _database;
  
  Future<Database> get database async {
    if (_database != null) return _database!;
    _database = await _initDatabase();
    return _database!;
  }
  
  Future<Database> _initDatabase() async {
    String path = await getDatabasesPath();
    String dbPath = join(path, 'gatherly.sqlite');
    return await openDatabase(dbPath, version: 1, onCreate: _createDatabase);
  }
  
  Future<void> _createDatabase(Database db, int version) async {
    // 与 Swift 版本完全一致的建表语句
    await db.execute('''
      CREATE TABLE items (
        id TEXT PRIMARY KEY,
        title TEXT,
        body TEXT,
        original_url TEXT NOT NULL,
        platform TEXT NOT NULL,
        platform_content_id TEXT,
        normalized_url TEXT NOT NULL,
        author TEXT,
        author_id TEXT,
        publish_date REAL,
        import_date REAL NOT NULL,
        modify_date REAL NOT NULL,
        content_status TEXT NOT NULL DEFAULT 'normal',
        archive_status TEXT NOT NULL DEFAULT 'pending',
        media_status TEXT NOT NULL DEFAULT 'textOnly',
        cover_asset_id TEXT,
        folder_id TEXT,
        remark TEXT,
        is_starred INTEGER NOT NULL DEFAULT 0,
        version INTEGER NOT NULL DEFAULT 1,
        deleted_at REAL,
        custom_platform_id TEXT
      )
    ''');
    // ... 其他表结构与 Swift 版本完全一致
  }
}
```

### 路径处理

```dart
// 跨平台路径处理
import 'package:path_provider/path_provider.dart';
import 'package:path/path.dart' as p;

Future<String> getDataDirectory() async {
  if (Platform.isMacOS) {
    final appSupport = await getApplicationSupportDirectory();
    return p.join(appSupport.path, 'com.gatherly.app');
  } else if (Platform.isWindows) {
    final appSupport = await getApplicationSupportDirectory();
    return p.join(appSupport.path, 'com.gatherly.app');
  }
  throw UnsupportedError('不支持的平台');
}

// 数据库中存储相对路径，运行时转换
String toStoredPath(String absolutePath, String dataDir) {
  return p.relative(absolutePath, from: dataDir);
}
```

---

## 五、UI 设计（macOS vs Windows）

### 5.1 macOS 原生风格（macos_ui）

```dart
// lib/views/app_shell.dart
import 'package:macos_ui/macos_ui.dart';

class AppShell extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return MacosWindow(
      sidebar: Sidebar(
        minWidth: 220,
        builder: (context, scrollController) {
          return SidebarItems(
            items: [
              SidebarItem(
                label: Text('首页'),
                leading: MacosIcon(CupertinoIcons.house),
              ),
              SidebarItem(
                label: Text('抖音'),
                leading: MacosIcon(CupertinoIcons.music_note),
              ),
              // ... 10 个平台
              SidebarItem(
                label: Text('搜索'),
                leading: MacosIcon(CupertinoIcons.search),
              ),
              SidebarItem(
                label: Text('回收站'),
                leading: MacosIcon(CupertinoIcons.trash),
              ),
              SidebarItem(
                label: Text('设置'),
                leading: MacosIcon(CupertinoIcons.gear),
              ),
            ],
          );
        },
      ),
      child: /* 右侧内容区 */,
    );
  }
}
```

### 5.2 Windows 风格（fluent_ui）

```dart
// lib/views/app_shell.dart
import 'package:fluent_ui/fluent_ui.dart';

class AppShell extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return NavigationView(
      pane: NavigationPane(
        selected: _selectedIndex,
        onChanged: (index) => setState(() => _selectedIndex = index),
        displayMode: PaneDisplayMode.auto,
        items: [
          PaneItem(
            icon: Icon(FluentIcons.home),
            title: Text('首页'),
          ),
          PaneItem(
            icon: Icon(Icons.music_note),
            title: Text('抖音'),
          ),
          // ... 10 个平台
          PaneItem(
            icon: Icon(FluentIcons.search),
            title: Text('搜索'),
          ),
          PaneItem(
            icon: Icon(FluentIcons.delete),
            title: Text('回收站'),
          ),
          PaneItem(
            icon: Icon(FluentIcons.settings),
            title: Text('设置'),
          ),
        ],
      ),
      content: /* 右侧内容区 */,
    );
  }
}
```

### 5.3 条件渲染策略

```dart
// 根据平台自动选择 UI 组件
import 'dart:io' show Platform;

Widget buildSidebar() {
  if (Platform.isMacOS) {
    return _buildMacosSidebar();  // macos_ui
  } else if (Platform.isWindows) {
    return _buildWindowsSidebar();  // fluent_ui
  }
  return _buildFallbackSidebar();
}

// 或者使用 abstract class + 工厂模式
abstract class AppShellDelegate {
  Widget buildSidebar(List<PlatformItem> items);
  Widget buildToolbar(PlatformItem platform);
  Widget buildCard(Item item);
}

class MacosShellDelegate implements AppShellDelegate {
  // 使用 macos_ui 组件
}

class WindowsShellDelegate implements AppShellDelegate {
  // 使用 fluent_ui 组件
}
```

---

## 六、项目目录结构

```
gatherly/
├── lib/
│   ├── main.dart                    应用入口
│   ├── app.dart                     MaterialApp 配置
│   ├── models/                      数据模型
│   │   ├── item.dart
│   │   ├── folder.dart
│   │   ├── media_asset.dart
│   │   ├── custom_platform.dart
│   │   ├── trash_record.dart
│   │   ├── import_task.dart
│   │   └── enums/
│   │       ├── platform.dart
│   │       ├── content_status.dart
│   │       ├── archive_status.dart
│   │       ├── media_status.dart
│   │       ├── media_type.dart
│   │       ├── download_status.dart
│   │       └── task_status.dart
│   ├── database/                    数据库层
│   │   ├── database_helper.dart
│   │   └── repositories/
│   │       ├── item_repository.dart
│   │       ├── folder_repository.dart
│   │       ├── media_repository.dart
│   │       ├── search_repository.dart
│   │       ├── trash_repository.dart
│   │       └── platform_repository.dart
│   ├── parsers/                     解析器
│   │   ├── content_parser.dart      abstract class
│   │   ├── base_parser.dart         基础实现
│   │   ├── platform_router.dart
│   │   ├── douyin_parser.dart
│   │   ├── xiaohongshu_parser.dart
│   │   ├── coolapk_parser.dart
│   │   ├── bilibili_parser.dart
│   │   ├── github_parser.dart
│   │   ├── youtube_parser.dart
│   │   ├── twitter_parser.dart
│   │   ├── weibo_parser.dart
│   │   ├── zhihu_parser.dart
│   │   └── douban_parser.dart
│   ├── services/                    服务层
│   │   ├── import_service.dart
│   │   ├── backup_service.dart
│   │   └── update_checker.dart
│   ├── utils/                       工具类
│   │   ├── url_normalizer.dart
│   │   ├── data_directory.dart
│   │   ├── browser_detector.dart
│   │   ├── platform_customization.dart
│   │   ├── media_exporter.dart
│   │   ├── file_picker_helper.dart
│   │   ├── js_web_loader.dart
│   │   └── viewer_window_manager.dart
│   ├── views/                       视图层
│   │   ├── home/
│   │   │   └── home_view.dart
│   │   ├── platform/
│   │   │   ├── platform_view.dart
│   │   │   ├── folder_view.dart
│   │   │   ├── custom_platform_view.dart
│   │   │   ├── uncategorized_view.dart
│   │   │   ├── new_platform_sheet.dart
│   │   │   ├── edit_platform_sheet.dart
│   │   │   └── change_logo_sheet.dart
│   │   ├── item/
│   │   │   ├── item_detail_view.dart
│   │   │   ├── edit_item_view.dart
│   │   │   └── new_item_view.dart
│   │   ├── search/
│   │   │   └── search_view.dart
│   │   ├── trash/
│   │   │   └── trash_view.dart
│   │   ├── settings/
│   │   │   ├── settings_view.dart
│   │   │   └── help_view.dart
│   │   └── components/
│   │       ├── item_card.dart
│   │       ├── markdown_viewer.dart
│   │       ├── markdown_editor.dart
│   │       ├── image_viewer.dart
│   │       ├── video_player.dart
│   │       ├── video_viewer.dart
│   │       ├── video_thumbnail.dart
│   │       ├── toast.dart
│   │       ├── export_picker.dart
│   │       └── placeholder_editor.dart
│   └── providers/                   Riverpod 状态管理
│       ├── items_provider.dart
│       ├── platforms_provider.dart
│       ├── search_provider.dart
│       └── settings_provider.dart
├── macos/                           macOS 平台特定代码
│   └── Runner/
├── windows/                         Windows 平台特定代码
│   └── runner/
├── pubspec.yaml                     依赖配置
└── README.md                        项目说明
```

---

## 七、执行步骤（按顺序）

### 阶段 1：项目初始化 + 数据层（预计 2-3 天）

1. 创建 Flutter 项目：`flutter create gatherly`
2. 配置 pubspec.yaml 依赖
3. 实现数据模型（13 个文件）
4. 实现数据库层（7 个 Repository）
5. 验证：数据库创建、CRUD 操作正常

### 阶段 2：解析器（预计 3-4 天）

6. 实现 ContentParser abstract class
7. 实现 BaseParser 基础实现
8. 实现 PlatformRouter
9. 实现 10 个平台解析器（逐个验证）
10. 验证：每个平台的链接能正常解析

### 阶段 3：服务层 + 工具层（预计 2-3 天）

11. 实现 ImportService
12. 实现 BackupService
13. 实现 UpdateChecker
14. 实现工具类（URLNormalizer、DataDirectory、BrowserDetector 等）
15. 验证：导入、备份、更新功能正常

### 阶段 4：macOS UI（预计 4-5 天）

16. 配置 macos_ui 依赖
17. 实现 AppShell（macOS Sidebar）
18. 实现 HomeView（粘贴导入 + 最近 7 天）
19. 实现 PlatformView（网格/列表 + 多选批量）
20. 实现 FolderView（二级文件夹）
21. 实现 ItemDetailView（Markdown + 图片 + 视频）
22. 实现 EditItemView（Markdown 编辑器 + 媒体增删）
23. 实现 SearchView（全文检索 + 筛选）
24. 实现 TrashView（30 天倒计时）
25. 实现 SettingsView（浏览器/目录/备份/更新/帮助）
26. 实现通用组件（ItemCard、ImageViewer、VideoPlayer 等）
27. 验证：macOS 上所有功能正常

### 阶段 5：Windows UI + 发布（预计 3-4 天）

28. 配置 fluent_ui 依赖
29. 实现 AppShell（Windows NavigationView）
30. 适配所有页面的 Windows 风格
31. 配置 Windows 构建（图标、安装器）
32. 构建 Windows 版本
33. 验证：Windows 上所有功能正常
34. 创建 GitHub Release v1.0.0
35. 推送代码到 V-Linkin/Gatherly

---

## 八、参考代码位置

参考代码（macOS Swift 版本）：
- GitHub：https://github.com/V-Linkin/Archiver
- 克隆到 /tmp/archiver-reference 作为参考

**迁移时的对应关系：**
- Swift 的 `struct` → Dart 的 `class`
- Swift 的 `enum` → Dart 的 `enum`
- Swift 的 `protocol` → Dart 的 `abstract class`
- Swift 的 `async/await` → Dart 的 `async/await`（语法一致）
- Swift 的 `URLSession` → Dart 的 `dio`
- Swift 的 `GRDB` → Dart 的 `sqflite`
- Swift 的 `SwiftUI` → Dart 的 `Flutter Widget`

---

## 九、注意事项

1. **数据库兼容** — sqflite 与 GRDB 的 FTS5 语法可能有差异，需要逐一验证
2. **WebView 差异** — webview_flutter 在 macOS 用 WKWebView，Windows 用 WebView2
3. **文件路径** — sqflite 的数据库路径在 macOS 和 Windows 上不同，用 path_provider 获取
4. **图标映射** — SwiftUI 的 SF Symbols → Flutter 的 Icons/macos_ui Icons
5. **异步处理** — Dart 和 Swift 的 async/await 语法几乎一致，迁移很顺滑
6. **状态管理** — Swift 用 @Observable/@Published，Dart 用 Riverpod/ChangeNotifier
7. **构建产物** — macOS 输出 .dmg，Windows 输出 .exe 安装包
8. **版本号** — 跨平台版本统一从 v1.0.0 开始
9. **酷安 WebView** — webview_flutter 需要验证酷安 JS 渲染是否正常
10. **视频播放** — video_player 在 macOS 用 AVPlayer，Windows 用 MediaPlayer，需要测试兼容性
11. **多窗口** — Flutter 的多窗口支持有限，可能需要 platform_channel 实现
12. **图标** — macOS 用 SF Symbols，Windows 用 Fluent Icons，需要分别映射
