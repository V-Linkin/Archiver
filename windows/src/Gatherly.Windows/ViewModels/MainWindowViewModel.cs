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
    public string DisplayPlatform => SelectedItem?.Platform.ToString() ?? "";
    public string DisplayPublishDate => SelectedItem?.PublishDate?.ToString("yyyy-MM-dd HH:mm") ?? "未知";
    public string DisplayImportDate => SelectedItem?.ImportDate.ToString("yyyy-MM-dd HH:mm") ?? "";
    public string DisplayRemark => SelectedItem?.Remark ?? "";
    public string DisplayOriginalUrl => SelectedItem?.OriginalUrl ?? "";
    public string DisplayNormalizedUrl => SelectedItem?.NormalizedUrl ?? "";

    public HomeViewModel Home { get; }
    public ContentListViewModel ContentList { get; }
    public SearchViewModel Search { get; }
    public TrashViewModel Trash { get; }

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

        _itemService = new ItemService(itemRepo, trashRepo);
        _backupImportService = new BackupImportService();
        _mediaRepo = mediaRepo;

        Home = new HomeViewModel(new HomeDataService(itemRepo, mediaRepo));
        ContentList = new ContentListViewModel(new ContentListService(itemRepo, folderRepo));
        Search = new SearchViewModel(new SearchService(searchRepo));
        Trash = new TrashViewModel(new TrashDataService(itemRepo, trashRepo), _itemService);

        // Subscribe to sub-ViewModel selection changes
        Home.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.SelectedItem))
                SelectedItem = Home.SelectedItem;
        };
        ContentList.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContentListViewModel.SelectedItem))
                SelectedItem = ContentList.SelectedItem;
        };
        Search.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SearchViewModel.SelectedItem))
                SelectedItem = Search.SelectedItem;
        };
        Trash.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrashViewModel.SelectedItem))
                SelectedItem = Trash.SelectedItem;
        };

        // Load home data on startup
        _ = Home.LoadCommand.ExecuteAsync(null);
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

            // Clear selection
            SelectedItem = null;
            Home.SelectedItem = null;
            ContentList.SelectedItem = null;
            Search.SelectedItem = null;
            Trash.SelectedItem = null;

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
    private void ShowHome() => CurrentSection = "Home";

    [RelayCommand]
    private void ShowSearch() => CurrentSection = "Search";

    [RelayCommand]
    private void ShowTrash() => CurrentSection = "Trash";

    [RelayCommand]
    private async Task TrashSelectedItemAsync()
    {
        if (SelectedItem == null) return;

        try
        {
            await _itemService.TrashItemAsync(SelectedItem);

            SelectedItem = null;
            Home.SelectedItem = null;
            ContentList.SelectedItem = null;
            Search.SelectedItem = null;
            Trash.SelectedItem = null;

            await Home.LoadCommand.ExecuteAsync(null);
            await Trash.LoadCommand.ExecuteAsync(null);
        }
        catch
        {
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
