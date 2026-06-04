# Windows 项目结构规划

## 1. 项目目录

```text
windows/
  Gatherly.Windows.sln
  src/
    Gatherly.Windows/
      Gatherly.Windows.csproj
      App.axaml                     # Avalonia 应用入口
      App.axaml.cs
      MainWindow.axaml              # 主窗口
      MainWindow.axaml.cs
      Program.cs                     # .NET 入口
      
      Models/                        # 数据模型（对应 shared/model/）
        Item.cs
        Folder.cs
        MediaAsset.cs
        CustomPlatform.cs
        TrashRecord.cs
        Enums/
          Platform.cs
          ArchiveStatus.cs
          ContentStatus.cs
          MediaStatus.cs
          MediaType.cs
          DownloadStatus.cs
          TaskStatus.cs
      
      ViewModels/                    # MVVM ViewModel
        MainViewModel.cs
        HomeViewModel.cs
        PlatformViewModel.cs
        FolderViewModel.cs
        ItemDetailViewModel.cs
        SearchViewModel.cs
        TrashViewModel.cs
        SettingsViewModel.cs
      
      Views/                         # Avalonia XAML 视图
        Home/
          HomeView.axaml
        Platform/
          PlatformView.axaml
          FolderView.axaml
          CustomPlatformContentView.axaml
          UncategorizedContentView.axaml
        Item/
          ItemDetailView.axaml
          EditItemView.axaml
          NewItemView.axaml
        Search/
          SearchResultsView.axaml
        Trash/
          TrashView.axaml
        Settings/
          SettingsView.axaml
      
      Services/                      # 业务服务层
        ItemService.cs
        FolderService.cs
        SearchService.cs
        ImportService.cs
        BackupService.cs
      
      Database/                      # 数据访问层
        DatabaseManager.cs
        ItemRepository.cs
        FolderRepository.cs
        MediaRepository.cs
        TrashRepository.cs
        SearchRepository.cs
        CustomPlatformRepository.cs
        Migrations/
          v1_create_tables.sql       # 从 shared/db/migrations/ 复制
          v2_fts.sql
      
      Utilities/
        URLNormalizer.cs
        DataDirectory.cs
        MediaExporter.cs
      
      Resources/                     # 资源文件
        Styles/
         .axaml
      
  tests/
    Gatherly.Windows.Tests/
      Gatherly.Windows.Tests.csproj
      Database/
        ItemRepositoryTests.cs
        FolderRepositoryTests.cs
      Services/
        ItemServiceTests.cs
      URL/
        URLNormalizerTests.cs
```

## 2. 引用 shared 契约

Windows 端不直接引用 shared/ 目录下的文件作为源码，而是按契约实现 C# 版本。

```text
shared/db/migrations/       → 复制 SQL 文件到 Database/Migrations/ 并作为 EmbeddedResource
shared/model/enums.json     → 手动或生成 C# enum
shared/model/*.schema.json  → 手动或生成 C# Model
shared/url/rules.json       → 实现 C# URLNormalizer
shared/parsers/fixtures/    → 用于 Parser 单元测试
shared/import-export/       → 实现 C# 导入/导出逻辑
```

## 3. .csproj 关键配置

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.1.*" />
    <PackageReference Include="Avalonia.Desktop" Version="11.1.*" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.1.*" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
    <PackageReference Include="Microsoft.Data.Sqlite" Version="8.*" />
  </ItemGroup>
  
  <ItemGroup>
    <EmbeddedResource Include="Database\Migrations\*.sql" />
  </ItemGroup>
</Project>
```

## 4. macOS 与 Windows 代码对比

| 模块 | macOS | Windows |
|------|-------|---------|
| UI 框架 | SwiftUI | Avalonia |
| 架构 | View + @Environment(AppState) | MVVM (ViewModel) |
| 数据库 | GRDB 7 | Microsoft.Data.Sqlite |
| FTS5 | GRDB FTS5 | SQLite FTS5 直接 SQL |
| 网络 | URLSession | HttpClient |
| WebView | WKWebView / JSWebLoader | WebView2 |
| 图片 | NSImage | Avalonia IImage |
| 文件选择 | NSSavePanel / NSOpenPanel | Avalonia StorageProvider |
| 系统集成 | NSWorkspace / MenuBar | Windows API / SystemTray |

## 5. 关键实现差异

```text
1. AppState → 多个独立 ViewModel
   macOS: 单一 AppState 作为上帝对象
   Windows: MainViewModel + 各页面 ViewModel

2. @Environment(AppState) → DI 注入
   macOS: SwiftUI 环境传递
   Windows: Microsoft.Extensions.DependencyInjection

3. GRDB DatabaseQueue → Microsoft.Data.Sqlite SqliteConnection
   macOS: GRDB 的类型安全查询
   Windows: 原始 SQL 或 Dapper

4. DispatchQueue.global → async/await
   macOS: GCD 异步
   Windows: Task.Run / async/await

5. UserDefaults → Windows 注册表或本地配置文件
```
