using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.Services.Backup;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 主窗口 ViewModel — 根容器，持有子 ViewModel、导航状态、选中项和备注编辑
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Gatherly Windows";

    [ObservableProperty]
    private string _currentSection = "Home";

    [ObservableProperty]
    private string _previousSection = "Home";

    [ObservableProperty]
    private Item? _selectedItem;

    [ObservableProperty]
    private string _editableRemark = string.Empty;

    [ObservableProperty]
    private bool _isEditingRemark;

    [ObservableProperty]
    private bool _isImportingBackup;

    [ObservableProperty]
    private string? _backupImportStatus;

    [ObservableProperty]
    private string? _backupImportError;

    public bool HasBackupImportStatus => BackupImportStatus != null;
    public bool HasBackupImportError => BackupImportError != null;

    /// <summary>
    /// 当前选中 item 的图片资产（本地路径已解析）
    /// </summary>
    public ObservableCollection<MediaAssetDisplay> ImageAssets { get; } = new();

    /// <summary>
    /// 当前选中 item 的视频资产
    /// </summary>
    public ObservableCollection<MediaAssetDisplay> VideoAssets { get; } = new();

    /// <summary>
    /// 合并的媒体资产（图片 + 视频），用于横向画廊展示
    /// </summary>
    public ObservableCollection<MediaAssetDisplay> AllMediaAssets { get; } = new();

    /// <summary>
    /// 正文中的链接列表
    /// </summary>
    public ObservableCollection<ContentLinkDisplay> DisplayBodyLinks { get; } = new();

    public bool HasDisplayBodyLinks => DisplayBodyLinks.Count > 0;

    public bool HasImages => ImageAssets.Count > 0;
    public bool HasVideos => VideoAssets.Count > 0;

    /// <summary>
    /// 视频预览卡片的封面图片路径（优先使用 cover asset，fallback 到第一张 image）
    /// </summary>
    public string? VideoCoverImagePath { get; private set; }

    /// <summary>
    /// 回收站操作成功后的回调（由 MainWindowViewModel 订阅以刷新 Sidebar）
    /// </summary>
    public Func<Task>? OnTrashOperationSuccess { get; set; }

    /// <summary>
    /// 内容变更后的回调（刷新 Sidebar 和当前平台页）
    /// </summary>
    public Func<Task>? OnContentChanged { get; set; }

    partial void OnBackupImportStatusChanged(string? value)
    {
        OnPropertyChanged(nameof(HasBackupImportStatus));
    }

    partial void OnBackupImportErrorChanged(string? value)
    {
        OnPropertyChanged(nameof(HasBackupImportError));
    }

    partial void OnSelectedItemChanged(Item? value)
    {
        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(DisplayAuthor));
        OnPropertyChanged(nameof(DisplayBody));
        OnPropertyChanged(nameof(HasDisplayBody));
        OnPropertyChanged(nameof(DisplayPlatform));
        OnPropertyChanged(nameof(DisplayPublishDate));
        OnPropertyChanged(nameof(DisplayImportDate));
        OnPropertyChanged(nameof(DisplayRemark));
        OnPropertyChanged(nameof(DisplayOriginalUrl));
        OnPropertyChanged(nameof(DisplayNormalizedUrl));
        OnPropertyChanged(nameof(HasPublishDate));
        _ = LoadFolderNameAsync(value?.FolderId);

        // Sync editable remark
        EditableRemark = value?.Remark ?? string.Empty;
        IsEditingRemark = false;

        // Parse body links
        ParseBodyLinks(value?.Body);

        // Load media assets for the selected item
        _ = LoadMediaAssetsAsync(value?.Id);
    }

    /// <summary>
    /// 解析正文中的链接
    /// </summary>
    private void ParseBodyLinks(string? body)
    {
        DisplayBodyLinks.Clear();
        if (string.IsNullOrWhiteSpace(body)) return;

        var segments = ContentParser.ParseSegments(body);
        foreach (var segment in segments)
        {
            if (segment.IsLink && segment.Url != null)
                DisplayBodyLinks.Add(new ContentLinkDisplay(segment.Url));
        }
        OnPropertyChanged(nameof(HasDisplayBodyLinks));
    }

    public bool HasSelectedItem => SelectedItem != null;

    public string DisplayTitle => SelectedItem?.Title ?? "未命名内容";
    public string DisplayAuthor => SelectedItem?.Author ?? "未知作者";
    public string DisplayBody => SelectedItem?.Body ?? "";
    public bool HasDisplayBody => !string.IsNullOrWhiteSpace(SelectedItem?.Body);

    public bool HasMedia => HasImages || HasVideos;
    public string DisplayPlatform => SelectedItem?.DisplayPlatform ?? "";
    public string DisplayPublishDate => SelectedItem?.PublishDate?.ToString("yyyy-MM-dd HH:mm") ?? "";
    public string DisplayImportDate => SelectedItem?.ImportDate.ToString("yyyy-MM-dd HH:mm") ?? "";
    public string DisplayRemark => SelectedItem?.Remark ?? "";
    public string DisplayOriginalUrl => SelectedItem?.OriginalUrl ?? "";
    public string DisplayNormalizedUrl => SelectedItem?.NormalizedUrl ?? "";
    public bool HasPublishDate => SelectedItem?.PublishDate != null;

    private string _displayFolderName = "";
    public string DisplayFolderName
    {
        get => _displayFolderName;
        private set { _displayFolderName = value; OnPropertyChanged(); }
    }

    private async Task LoadFolderNameAsync(Guid? folderId)
    {
        if (folderId == null)
        {
            DisplayFolderName = "";
            OnPropertyChanged(nameof(HasFolder));
            return;
        }
        try
        {
            var folderRepo = new FolderRepository(_connection);
            var folder = await folderRepo.GetByIdAsync(folderId.Value);
            DisplayFolderName = folder?.Name ?? "";
        }
        catch
        {
            DisplayFolderName = "";
        }
        OnPropertyChanged(nameof(HasFolder));
    }

    public bool HasFolder => !string.IsNullOrEmpty(DisplayFolderName);

    public HomeViewModel Home { get; }
    public ContentListViewModel ContentList { get; }
    public SearchViewModel Search { get; }
    public TrashViewModel Trash { get; }
    public BackupPackageViewModel BackupVM { get; }
    public SettingsViewModel Settings { get; }

    /// <summary>
    /// Sidebar 平台入口列表
    /// </summary>
    public ObservableCollection<PlatformEntryDisplay> SidebarPlatforms { get; } = new();

    [ObservableProperty]
    private string _platformTitle = "";

    private readonly ItemService _itemService;
    private readonly Services.FolderService _folderService;
    private readonly BackupImportService _backupImportService;
    private readonly MediaRepository _mediaRepo;
    private readonly CustomPlatformRepository _customPlatformRepo;
    private readonly SystemPlatformDisplayNames _systemPlatformDisplayNames;
    private readonly SqliteConnection _connection;

    /// <summary>
    /// App 主连接 — 供 EditItemWindow 等外部组件使用同一数据库
    /// </summary>
    public SqliteConnection MainConnection => _connection;
    private readonly Task _migrationTask;

    public MainWindowViewModel(SqliteConnection connection, string? dataDirectory = null)
    {
        _connection = connection;
        var itemRepo = new ItemRepository(connection);
        var folderRepo = new FolderRepository(connection);
        var searchRepo = new SearchRepository(connection);
        var trashRepo = new TrashRepository(connection);
        var mediaRepo = new MediaRepository(connection);
        var importTaskRepo = new ImportTaskRepository(connection);

        _itemService = new ItemService(itemRepo, trashRepo, folderRepo, mediaRepo, connection);
        _folderService = new Services.FolderService(folderRepo, itemRepo);
        _backupImportService = new BackupImportService();
        _mediaRepo = mediaRepo;

        var customPlatformRepo = new CustomPlatformRepository(connection);
        _customPlatformRepo = customPlatformRepo;
        _systemPlatformDisplayNames = new SystemPlatformDisplayNames(dataDirectory);

        // System-to-Custom migration
        var customMap = new Services.SystemPlatformCustomMap(dataDirectory);
        var migrationService = new Services.SystemPlatformItemMigrationService(connection, customPlatformRepo, customMap, _systemPlatformDisplayNames);
        _migrationTask = MigrateAsync(migrationService);

        Home = new HomeViewModel(new HomeDataService(itemRepo, mediaRepo, customPlatformRepo, connection), new ImportService(itemRepo, importTaskRepo, new Services.Media.MediaDownloadService(mediaRepo), TimeProvider.System, customMap), searchRepo, mediaRepo, customPlatformRepo);
        ContentList = new ContentListViewModel(new ContentListService(itemRepo, folderRepo), mediaRepo, customPlatformRepo);
        Search = new SearchViewModel(new SearchService(searchRepo), mediaRepo, customPlatformRepo);
        Trash = new TrashViewModel(new TrashDataService(itemRepo, trashRepo), _itemService);
        BackupVM = new BackupPackageViewModel(() => new BackupPackageServiceAdapter(new BackupPackageV2Service(connection, customPlatformRepo, _systemPlatformDisplayNames, customMap)));
        Settings = new SettingsViewModel(BackupVM);

        // Subscribe to sub-ViewModel selection changes → navigate to detail
        // Note: Trash does NOT navigate to detail (macOS behavior: trash only selects)
        Home.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.SelectedItem) && Home.SelectedItem != null)
                NavigateToDetail(Home.SelectedItem, "Home");
        };
        ContentList.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContentListViewModel.SelectedItem) && ContentList.SelectedItem != null)
                NavigateToDetail(ContentList.SelectedItem, CurrentSection == "PlatformContent" ? "PlatformContent" : "Home");
        };
        Search.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SearchViewModel.SelectedItem) && Search.SelectedItem != null)
                NavigateToDetail(Search.SelectedItem, "Search");
        };

        Search.OnItemSelected = item =>
        {
            NavigateToDetail(item, "Search");
        };
        Search.OnMoveToPlatformRequested = item => HandleMoveToPlatform(item);
        Search.OnMoveToFolderRequested = item => HandleMoveToFolder(item);
        Search.OnDeleteItemRequested = async item =>
        {
            try
            {
                await _itemService.TrashItemAsync(item);
                await Search.SearchCommand.ExecuteAsync(null);
            }
            catch { }
        };
        // Trash selection stays in trash view — no navigation to detail

        // Load home data and sidebar platforms on startup
        _ = Home.LoadCommand.ExecuteAsync(null);
        _ = LoadSidebarPlatformsAsync();

        // 订阅导入成功回调，刷新 Sidebar
        Home.OnImportSuccess = async () =>
        {
            await LoadSidebarPlatformsAsync();
            if (CurrentSection == "PlatformContent")
                await ContentList.ReloadCurrentContentAsync();
        };

        Home.OnPlatformEntryClicked = entry =>
        {
            if (entry.IsUncategorized)
                _ = ShowUncategorizedCommand.ExecuteAsync(null);
            else if (entry.IsStandardPlatform && entry.StandardPlatform.HasValue && entry.CustomPlatformIds.Count > 0)
                _ = ShowMergedPlatformCommand.ExecuteAsync(entry);
            else if (entry.IsStandardPlatform && entry.StandardPlatform.HasValue && entry.CustomPlatformIds.Count == 0)
                _ = ShowStandardPlatformCommand.ExecuteAsync(entry.StandardPlatform.Value);
            else if (entry.CustomPlatformIds.Count == 1)
                _ = ShowCustomPlatformCommand.ExecuteAsync(entry.CustomPlatformIds[0]);
            else
                _ = ShowCustomPlatformCommand.ExecuteAsync(entry.Id);
        };

        Home.OnItemSelected = item =>
        {
            NavigateToDetail(item, "Home");
        };

        Home.OnHideItemRequested = item =>
        {
            var hiddenFile = Path.Combine(
                Gatherly.Windows.Database.DatabasePaths.DataDirectory, "hidden_items.txt");
            try
            {
                var ids = File.Exists(hiddenFile)
                    ? new HashSet<string>(File.ReadAllLines(hiddenFile))
                    : new HashSet<string>();
                ids.Add(item.Id.ToString("D"));
                File.WriteAllLines(hiddenFile, ids);
            }
            catch { }
            // 立即从集合移除，不刷新整个首页
            Home.RecentItems.Remove(item);
        };

        Home.OnDeleteItemRequested = async item =>
        {
            try
            {
                await _itemService.TrashItemAsync(item);
                Home.RecentItems.Remove(item);
                Home.SearchResults.Remove(item);
                await LoadSidebarPlatformsAsync();
            }
            catch { }
        };

        Home.OnMoveToPlatformRequested = item => HandleMoveToPlatform(item);
        Home.OnMoveToFolderRequested = item => HandleMoveToFolder(item);

        // 订阅回收站操作成功回调，刷新 Sidebar
        Trash.OnTrashOperationSuccess = LoadSidebarPlatformsAsync;

        // 订阅内容变更回调，刷新 Sidebar 和平台页
        OnContentChanged = async () =>
        {
            await LoadSidebarPlatformsAsync();
            if (CurrentSection == "PlatformContent")
                await ContentList.ReloadCurrentContentAsync();
        };

        // 订阅移动到平台请求
        ContentList.OnMoveToPlatformRequested = item => HandleMoveToPlatform(item);

        // 订阅文件夹操作
        ContentList.OnNewFolderRequested = () => HandleNewFolder();
        ContentList.OnFolderClicked = folder => HandleFolderClicked(folder);
        ContentList.OnFolderRenameRequested = folder => HandleFolderRename(folder);
        ContentList.OnFolderDeleteRequested = folder => HandleFolderDelete(folder);
        ContentList.OnNavigateBackRequested = () => HandleNavigateBack();
        ContentList.OnItemSelected = item =>
        {
            var section = CurrentSection == "PlatformContent" ? "PlatformContent" : "Home";
            NavigateToDetail(item, section);
        };
        ContentList.OnMoveToFolderRequested = item => HandleMoveToFolder(item);
        ContentList.OnDeleteItemRequested = item => HandleDeleteItem(item);
    }

    private static Task MigrateAsync(Services.SystemPlatformItemMigrationService migrationService)
    {
        try
        {
            migrationService.Migrate();
        }
        catch
        {
            // Migration failure does not block app startup
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 等待 migration 完成（测试用）
    /// </summary>
    public Task WaitForMigrationAsync() => _migrationTask;

    /// <summary>
    /// 从 zip 备份包恢复数据
    /// </summary>
    public async Task ImportBackupAsync(string backupZipPath)
    {
        if (IsImportingBackup) return;

        IsImportingBackup = true;
        BackupImportStatus = null;
        BackupImportError = null;

        try
        {
            await _backupImportService.ImportBackupAsync(
                backupZipPath,
                DatabasePaths.DatabaseFile,
                DatabasePaths.DataDirectory);

            BackupImportStatus = "导入成功";

            // Clear selection and go back to Home
            SelectedItem = null;
            Home.SelectedItem = null;
            ContentList.SelectedItem = null;
            Search.SelectedItem = null;
            Trash.SelectedItem = null;
            CurrentSection = "Home";

            // Refresh data
            await Home.LoadCommand.ExecuteAsync(null);
            await Trash.LoadCommand.ExecuteAsync(null);

            if (!string.IsNullOrWhiteSpace(Search.Query))
            {
                await Search.SearchCommand.ExecuteAsync(null);
            }
        }
        catch (Exception ex)
        {
            BackupImportError = $"导入失败：{ex.Message}";
        }
        finally
        {
            IsImportingBackup = false;
        }
    }

    [RelayCommand]
    private async Task ShowHomeAsync()
    {
        CurrentSection = "Home";
        Search.Reset();
        await Home.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowSearch()
    {
        CurrentSection = "Search";
    }

    [RelayCommand]
    private async Task ShowTrashAsync()
    {
        CurrentSection = "Trash";
        await Trash.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowSettings()
    {
        CurrentSection = "Settings";
    }

    /// <summary>
    /// 加载 Sidebar 平台入口
    /// </summary>
    public async Task LoadSidebarPlatformsAsync()
    {
        var platforms = await Home.GetPlatformStatsAsync();
        SidebarPlatforms.Clear();
        foreach (var p in platforms)
            SidebarPlatforms.Add(p);
    }

    /// <summary>
    /// 打开平台管理窗口
    /// </summary>
    [RelayCommand]
    private void OpenPlatformManagement()
    {
        var platformService = new Services.CustomPlatformService(_customPlatformRepo);
        var itemRepo = new ItemRepository(_connection);
        var vm = new PlatformManagementViewModel(platformService, itemRepo);
        vm.OnPlatformChanged = RefreshAllPlatformViewsAsync;

        var window = new Views.PlatformManagementWindow
        {
            DataContext = vm
        };

        window.Show();
        _ = vm.LoadCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// 统一刷新所有平台视图（Sidebar + 首页平台入口）
    /// </summary>
    private async Task RefreshAllPlatformViewsAsync()
    {
        await LoadSidebarPlatformsAsync();
        await Home.LoadCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// 进入自定义平台内容页
    /// </summary>
    [RelayCommand]
    private async Task ShowCustomPlatformAsync(Guid platformId)
    {
        var platform = SidebarPlatforms.FirstOrDefault(p => p.Id == platformId);
        PlatformTitle = platform?.Name ?? "平台内容";
        ContentList.SetPageTitle(platform?.Name ?? "平台内容");
        PreviousSection = CurrentSection;
        CurrentSection = "PlatformContent";
        await ContentList.LoadCustomPlatformAsync(platformId);
    }

    /// <summary>
    /// 进入标准平台内容页
    /// </summary>
    [RelayCommand]
    private async Task ShowStandardPlatformAsync(Platform platform)
    {
        PlatformTitle = platform.GetDisplayName();
        ContentList.SetPageTitle(platform.GetDisplayName());
        PreviousSection = CurrentSection;
        CurrentSection = "PlatformContent";
        await ContentList.LoadPlatformAsync(platform);
    }

    /// <summary>
    /// 进入合并平台内容页（标准平台 + macOS 备份的自定义平台）
    /// </summary>
    [RelayCommand]
    private async Task ShowMergedPlatformAsync(PlatformEntryDisplay entry)
    {
        PlatformTitle = entry.Name;
        ContentList.SetPageTitle(entry.Name);
        PreviousSection = CurrentSection;
        CurrentSection = "PlatformContent";
        await ContentList.LoadMergedPlatformAsync(entry.StandardPlatform!.Value, entry.CustomPlatformIds);
    }

    /// <summary>
    /// 进入未分类内容页
    /// </summary>
    [RelayCommand]
    private async Task ShowUncategorizedAsync()
    {
        PlatformTitle = "未分类内容";
        ContentList.SetPageTitle("未分类内容");
        PreviousSection = CurrentSection;
        CurrentSection = "PlatformContent";
        await ContentList.LoadUncategorizedAsync();
    }

    /// <summary>
    /// 进入详情页
    /// </summary>
    public void NavigateToDetail(Item item, string fromSection)
    {
        PreviousSection = fromSection;
        SelectedItem = item;
        CurrentSection = "Detail";
    }

    /// <summary>
    /// 从详情页返回
    /// </summary>
    [RelayCommand]
    private void NavigateBack()
    {
        // Clear sub-ViewModel selections to avoid stale state
        Home.SelectedItem = null;
        Search.SelectedItem = null;
        Trash.SelectedItem = null;

        CurrentSection = PreviousSection;
    }

    [RelayCommand]
    private async Task TrashSelectedItemAsync()
    {
        if (SelectedItem == null) return;

        try
        {
            await _itemService.TrashItemAsync(SelectedItem);

            // Navigate back
            var previousSection = PreviousSection;
            CurrentSection = PreviousSection;
            SelectedItem = null;
            Home.SelectedItem = null;
            ContentList.SelectedItem = null;
            Search.SelectedItem = null;
            Trash.SelectedItem = null;

            await Home.LoadCommand.ExecuteAsync(null);
            await Trash.LoadCommand.ExecuteAsync(null);
            await LoadSidebarPlatformsAsync();

            // 刷新当前平台页内容
            if (OnContentChanged != null)
                await OnContentChanged();
        }
        catch (Exception ex)
        {
            // Show error to user via backup status area (reuse existing UI)
            BackupImportError = $"删除失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void StartEditRemark()
    {
        if (SelectedItem == null) return;
        EditableRemark = SelectedItem.Remark ?? string.Empty;
        IsEditingRemark = true;
    }

    [RelayCommand]
    private void CancelEditRemark()
    {
        EditableRemark = SelectedItem?.Remark ?? string.Empty;
        IsEditingRemark = false;
    }

    [RelayCommand]
    private async Task SaveRemarkAsync()
    {
        if (SelectedItem == null) return;

        try
        {
            var updated = await _itemService.UpdateRemarkAsync(SelectedItem, EditableRemark);
            SelectedItem = updated;
            IsEditingRemark = false;
        }
        catch
        {
        }
    }

    /// <summary>
    /// 加载选中 item 的媒体资产
    /// </summary>
    private async Task LoadMediaAssetsAsync(Guid? itemId)
    {
        ImageAssets.Clear();
        VideoAssets.Clear();
        AllMediaAssets.Clear();
        VideoCoverImagePath = null;
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(HasVideos));
        OnPropertyChanged(nameof(HasMedia));
        OnPropertyChanged(nameof(VideoCoverImagePath));

        if (itemId == null) return;

        try
        {
            var assets = await _mediaRepo.GetByItemIdAsync(itemId.Value);
            foreach (var asset in assets)
            {
                var display = MediaAssetDisplay.FromAsset(asset);
                if (display == null) continue;

                if (asset.Type == MediaType.image || asset.Type == MediaType.cover)
                    ImageAssets.Add(display);
                else if (asset.Type == MediaType.video)
                    VideoAssets.Add(display);
            }

            // 视频预览封面：优先 cover，fallback 到第一张 image
            if (HasVideos && VideoCoverImagePath == null)
            {
                var coverAsset = assets.FirstOrDefault(a => a.Type == MediaType.cover && !string.IsNullOrEmpty(a.LocalPath));
                if (coverAsset != null)
                {
                    var path = MediaPathHelper.ResolveFullPath(coverAsset.LocalPath);
                    if (File.Exists(path))
                        VideoCoverImagePath = path;
                }

                if (VideoCoverImagePath == null)
                {
                    var imgAsset = assets.FirstOrDefault(a => a.Type == MediaType.image && !string.IsNullOrEmpty(a.LocalPath));
                    if (imgAsset != null)
                    {
                        var path = MediaPathHelper.ResolveFullPath(imgAsset.LocalPath);
                        if (File.Exists(path))
                            VideoCoverImagePath = path;
                    }
                }

                OnPropertyChanged(nameof(VideoCoverImagePath));
            }

            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(HasVideos));
            OnPropertyChanged(nameof(HasMedia));

            // Build combined gallery: images first, then videos
            AllMediaAssets.Clear();
            foreach (var img in ImageAssets) AllMediaAssets.Add(img);
            foreach (var vid in VideoAssets) AllMediaAssets.Add(vid);
            OnPropertyChanged(nameof(AllMediaAssets));
        }
        catch
        {
            // 媒体加载失败不影响主流程
        }
    }

    /// <summary>
    /// 打开图片/视频 — 统一画廊点击处理
    /// </summary>
    [RelayCommand]
    private void OpenMedia(MediaAssetDisplay? media)
    {
        if (media == null) return;

        if (media.IsVideo)
        {
            OpenVideoFile(media);
        }
        else
        {
            OpenImageViewer(media);
        }
    }

    /// <summary>
    /// 打开图片查看器
    /// </summary>
    [RelayCommand]
    private void OpenImageViewer(MediaAssetDisplay? image)
    {
        if (image == null || ImageAssets.Count == 0) return;

        var index = ImageAssets.IndexOf(image);
        if (index < 0) index = 0;

        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Avalonia.Controls.Window mainWindow)
        {
            Views.ImageViewerWindow.Open(mainWindow, ImageAssets.ToList(), index);
        }
    }

    /// <summary>
    /// 用系统默认播放器打开视频文件
    /// </summary>
    [RelayCommand]
    private void OpenVideoFile(MediaAssetDisplay? video)
    {
        if (video?.FullPath == null || !video.FileExists) return;

        try
        {
            var fullPath = video.FullPath.Replace('/', '\\');
            if (File.Exists(fullPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fullPath,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // 打开失败不影响主流程
        }
    }

    /// <summary>
    /// 视频预览卡片点击 — 打开第一个视频
    /// </summary>
    [RelayCommand]
    private void PlayVideoCard()
    {
        var firstVideo = VideoAssets.FirstOrDefault(v => v.FileExists);
        if (firstVideo != null)
            OpenVideoFile(firstVideo);
    }

    /// <summary>
    /// 打开视频文件所在目录
    /// </summary>
    [RelayCommand]
    private void OpenVideoFolder(MediaAssetDisplay? video)
    {
        if (video?.FullPath == null) return;

        try
        {
            // 确保路径使用正确的反斜杠
            var fullPath = video.FullPath.Replace('/', '\\');

            if (video.FileExists && File.Exists(fullPath))
            {
                // 文件存在：用 explorer /select 定位文件
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{fullPath}\"",
                    UseShellExecute = false
                });
            }
            else
            {
                // 文件不存在：打开所在目录
                var dir = Path.GetDirectoryName(fullPath);
                if (dir != null && Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{dir}\"",
                        UseShellExecute = false
                    });
                }
            }
        }
        catch
        {
            // 打开失败不影响主流程
        }
    }

    /// <summary>
    /// 外部链接服务
    /// </summary>
    private readonly IExternalLinkService _externalLinkService = new ExternalLinkService();

    /// <summary>
    /// 打开外部链接
    /// </summary>
    [RelayCommand]
    private void OpenExternalLink(string? url)
    {
        _externalLinkService.Open(url);
    }

    /// <summary>
    /// 打开原始链接
    /// </summary>
    [RelayCommand]
    private void OpenOriginalUrl()
    {
        if (SelectedItem?.OriginalUrl != null)
            _externalLinkService.Open(SelectedItem.OriginalUrl);
    }

    private async void HandleMoveToPlatform(Item item)
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Avalonia.Controls.Window mainWindow)
        {
            var window = new Views.MoveToPlatformWindow(item, _customPlatformRepo)
            {
                Title = "移动到平台"
            };
            await window.ShowDialog(mainWindow);

            if (window.Result != null)
            {
                try
                {
                    await _itemService.MoveToCustomPlatformAsync(item, window.Result.CustomPlatformId, _customPlatformRepo);

                    await LoadSidebarPlatformsAsync();
                    if (CurrentSection == "PlatformContent")
                        await ContentList.ReloadCurrentContentAsync();
                }
                catch (Exception ex)
                {
                    BackupImportError = $"移动失败：{ex.Message}";
                }
            }
        }
    }

    private async void HandleNewFolder()
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Avalonia.Controls.Window mainWindow)
        {
            var dialog = new Views.NewFolderWindow();
            await dialog.ShowDialog(mainWindow);

            if (dialog.Result != null)
            {
                try
                {
                    var platform = ContentList.CurrentPagePlatform;
                    var cpId = ContentList.CurrentPageCustomPlatformId;
                    await _folderService.CreateFolderAsync(dialog.Result, platform, customPlatformId: cpId);
                    if (CurrentSection == "PlatformContent")
                        await ContentList.ReloadCurrentContentAsync();
                    await LoadSidebarPlatformsAsync();
                }
                catch (Exception ex)
                {
                    BackupImportError = $"创建文件夹失败：{ex.Message}";
                }
            }
        }
    }

    private async void HandleFolderClicked(Folder folder)
    {
        PlatformTitle = folder.Name;
        PreviousSection = CurrentSection;
        CurrentSection = "PlatformContent";
        await ContentList.LoadFolderAsync(folder.Id);
    }

    private async void HandleFolderRename(Folder folder)
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Avalonia.Controls.Window mainWindow)
        {
            var dialog = new Views.NewFolderWindow("重命名文件夹");
            await dialog.ShowDialog(mainWindow);

            if (dialog.Result != null)
            {
                try
                {
                    await _folderService.RenameFolderAsync(folder.Id, dialog.Result);
                    if (CurrentSection == "PlatformContent")
                        await ContentList.ReloadCurrentContentAsync();
                }
                catch (Exception ex)
                {
                    BackupImportError = $"重命名失败：{ex.Message}";
                }
            }
        }
    }

    private async void HandleFolderDelete(Folder folder)
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Avalonia.Controls.Window mainWindow)
        {
            var dialog = new Views.ConfirmDialogWindow("删除文件夹", $"确定删除文件夹「{folder.Name}」？内容不会被删除。");
            await dialog.ShowDialog(mainWindow);

            if (dialog.Result == true)
            {
                try
                {
                    await _folderService.DeleteFolderAsync(folder.Id);
                    if (CurrentSection == "PlatformContent")
                        await ContentList.ReloadCurrentContentAsync();
                    await LoadSidebarPlatformsAsync();
                }
                catch (Exception ex)
                {
                    BackupImportError = $"删除失败：{ex.Message}";
                }
            }
        }
    }

    private async void HandleNavigateBack()
    {
        // Clear folder state first, then reload parent platform view
        ContentList.ClearFolderState();
        PlatformTitle = "内容列表";
        await ContentList.ReloadCurrentContentAsync();
    }

    public async void HandleMoveToFolder(Item item)
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Avalonia.Controls.Window mainWindow)
        {
            // Get folders for current platform context
            var folderRepo = new FolderRepository(_connection);
            var platform = ContentList.CurrentPagePlatform;
            var cpId = ContentList.CurrentPageCustomPlatformId;

            List<Folder> folders;
            if (cpId.HasValue)
                folders = await folderRepo.GetByCustomPlatformIdAsync(cpId.Value);
            else
                folders = await folderRepo.GetByPlatformAsync(platform);

            if (folders.Count == 0)
            {
                BackupImportError = "当前平台没有文件夹，请先创建文件夹。";
                return;
            }

            var window = new Views.MoveToFolderWindow(folders);
            await window.ShowDialog(mainWindow);

            if (window.Result != null)
            {
                try
                {
                    await _itemService.MoveToFolderAsync(item, window.Result.FolderId, folderRepo);
                    if (CurrentSection == "PlatformContent")
                        await ContentList.ReloadCurrentContentAsync();
                }
                catch (Exception ex)
                {
                    BackupImportError = $"移动到文件夹失败：{ex.Message}";
                }
            }
        }
    }

    private async void HandleDeleteItem(Item item)
    {
        if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is Avalonia.Controls.Window mainWindow)
        {
            var dialog = new Views.ConfirmDialogWindow("删除内容", $"确定将「{item.Title ?? "未命名内容"}」移入回收站？");
            await dialog.ShowDialog(mainWindow);

            if (dialog.Result == true)
            {
                try
                {
                    await _itemService.TrashItemAsync(item);
                    // Reload current view (folder or platform)
                    await ContentList.ReloadCurrentContentAsync();
                    await LoadSidebarPlatformsAsync();
                }
                catch (Exception ex)
                {
                    BackupImportError = $"删除失败：{ex.Message}";
                }
            }
        }
    }
}

/// <summary>
/// 媒体资产展示模型
/// </summary>
public class MediaAssetDisplay
{
    public string FileName { get; set; } = "";
    public string? FullPath { get; set; }
    public bool FileExists { get; set; }
    public long FileSize { get; set; }
    public bool IsVideo { get; set; }
    public bool IsImage => !IsVideo;
    public string FileSizeDisplay => FileSize > 1024 * 1024
        ? $"{FileSize / 1024 / 1024:F1} MB"
        : $"{FileSize / 1024:F0} KB";

    public static MediaAssetDisplay? FromAsset(MediaAsset asset)
    {
        if (string.IsNullOrEmpty(asset.LocalPath)) return null;

        var fullPath = MediaPathHelper.ResolveFullPath(asset.LocalPath);
        var exists = File.Exists(fullPath);

        return new MediaAssetDisplay
        {
            FileName = asset.FileName,
            FullPath = exists ? fullPath : null,
            FileExists = exists,
            FileSize = asset.FileSize,
            IsVideo = asset.Type == MediaType.video
        };
    }
}
