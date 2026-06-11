using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
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

    public bool HasImages => ImageAssets.Count > 0;
    public bool HasVideos => VideoAssets.Count > 0;

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
        OnPropertyChanged(nameof(DisplayPlatform));
        OnPropertyChanged(nameof(DisplayPublishDate));
        OnPropertyChanged(nameof(DisplayImportDate));
        OnPropertyChanged(nameof(DisplayRemark));
        OnPropertyChanged(nameof(DisplayOriginalUrl));
        OnPropertyChanged(nameof(DisplayNormalizedUrl));

        // Sync editable remark
        EditableRemark = value?.Remark ?? string.Empty;
        IsEditingRemark = false;

        // Load media assets for the selected item
        _ = LoadMediaAssetsAsync(value?.Id);
    }

    public bool HasSelectedItem => SelectedItem != null;

    public string DisplayTitle => SelectedItem?.Title ?? "未命名内容";
    public string DisplayAuthor => SelectedItem?.Author ?? "未知作者";
    public string DisplayBody => SelectedItem?.Body ?? "";
    public string DisplayPlatform => SelectedItem?.DisplayPlatform ?? "";
    public string DisplayPublishDate => SelectedItem?.PublishDate?.ToString("yyyy-MM-dd HH:mm") ?? "未知";
    public string DisplayImportDate => SelectedItem?.ImportDate.ToString("yyyy-MM-dd HH:mm") ?? "";
    public string DisplayRemark => SelectedItem?.Remark ?? "";
    public string DisplayOriginalUrl => SelectedItem?.OriginalUrl ?? "";
    public string DisplayNormalizedUrl => SelectedItem?.NormalizedUrl ?? "";

    public HomeViewModel Home { get; }
    public ContentListViewModel ContentList { get; }
    public SearchViewModel Search { get; }
    public TrashViewModel Trash { get; }

    /// <summary>
    /// Sidebar 平台入口列表
    /// </summary>
    public ObservableCollection<PlatformEntryDisplay> SidebarPlatforms { get; } = new();

    [ObservableProperty]
    private string _platformTitle = "";

    private readonly ItemService _itemService;
    private readonly BackupImportService _backupImportService;
    private readonly MediaRepository _mediaRepo;

    public MainWindowViewModel(SqliteConnection connection)
    {
        var itemRepo = new ItemRepository(connection);
        var folderRepo = new FolderRepository(connection);
        var searchRepo = new SearchRepository(connection);
        var trashRepo = new TrashRepository(connection);
        var mediaRepo = new MediaRepository(connection);
        var importTaskRepo = new ImportTaskRepository(connection);

        _itemService = new ItemService(itemRepo, trashRepo, folderRepo);
        _backupImportService = new BackupImportService();
        _mediaRepo = mediaRepo;

        var customPlatformRepo = new CustomPlatformRepository(connection);
        Home = new HomeViewModel(new HomeDataService(itemRepo, mediaRepo, customPlatformRepo, connection), new ImportService(itemRepo, importTaskRepo, new Services.Media.MediaDownloadService(mediaRepo)));
        ContentList = new ContentListViewModel(new ContentListService(itemRepo, folderRepo), mediaRepo, customPlatformRepo);
        Search = new SearchViewModel(new SearchService(searchRepo), mediaRepo, customPlatformRepo);
        Trash = new TrashViewModel(new TrashDataService(itemRepo, trashRepo), _itemService);

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
        // Trash selection stays in trash view — no navigation to detail

        // Load home data and sidebar platforms on startup
        _ = Home.LoadCommand.ExecuteAsync(null);
        _ = LoadSidebarPlatformsAsync();
    }

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
    /// 进入自定义平台内容页
    /// </summary>
    [RelayCommand]
    private async Task ShowCustomPlatformAsync(Guid platformId)
    {
        var platform = SidebarPlatforms.FirstOrDefault(p => p.Id == platformId);
        PlatformTitle = platform?.Name ?? "平台内容";
        PreviousSection = CurrentSection;
        CurrentSection = "PlatformContent";
        await ContentList.LoadCustomPlatformAsync(platformId);
    }

    /// <summary>
    /// 进入未分类内容页
    /// </summary>
    [RelayCommand]
    private async Task ShowUncategorizedAsync()
    {
        PlatformTitle = "未分类内容";
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
            CurrentSection = PreviousSection;
            SelectedItem = null;
            Home.SelectedItem = null;
            ContentList.SelectedItem = null;
            Search.SelectedItem = null;
            Trash.SelectedItem = null;

            await Home.LoadCommand.ExecuteAsync(null);
            await Trash.LoadCommand.ExecuteAsync(null);
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
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(HasVideos));

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

            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(HasVideos));
        }
        catch
        {
            // 媒体加载失败不影响主流程
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
            FileSize = asset.FileSize
        };
    }
}
