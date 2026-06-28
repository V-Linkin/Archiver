using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 内容列表 ViewModel — 平台 / 文件夹 / 自定义平台 / 未分类
/// </summary>
public partial class ContentListViewModel : ViewModelBase
{
    private readonly ContentListService _contentService;
    private readonly MediaRepository _mediaRepo;
    private readonly CustomPlatformRepository _customPlatformRepo;

    public ObservableCollection<Item> Items { get; } = new();
    public ObservableCollection<Folder> Folders { get; } = new();

    [ObservableProperty]
    private Item? _selectedItem;

    /// <summary>
    /// 当前查看的平台类型
    /// </summary>
    private enum PlatformViewType { None, Standard, Merged, Custom, Uncategorized, Folder }

    private PlatformViewType _currentViewType = PlatformViewType.None;
    private PlatformViewType _savedViewType = PlatformViewType.None;
    private Platform? _currentPlatform;
    private List<Guid>? _currentCustomPlatformIds;
    private Guid? _currentCustomPlatformId;
    private Guid? _currentFolderId;

    /// <summary>
    /// 当前页面的平台（用于新建文件夹时确定 folder.platform 和 customPlatformID）
    /// </summary>
    public Platform CurrentPagePlatform => _currentPlatform ?? Platform.custom;
    public Guid? CurrentPageCustomPlatformId => _currentCustomPlatformId;

    /// <summary>
    /// 是否正在查看文件夹内容（显示返回按钮）
    /// </summary>
    public bool IsInFolder => _currentFolderId.HasValue;
    public Guid? CurrentFolderId => _currentFolderId;
    public string FolderTitle { get; private set; } = "";
    public string PageTitle { get; private set; } = "";

    /// <summary>
    /// 由 MainWindowViewModel 调用设置页面标题
    /// </summary>
    public void SetPageTitle(string title)
    {
        PageTitle = title;
        OnPropertyChanged(nameof(PageTitle));
    }

    public bool HasItems => Items.Count > 0;
    public bool HasFolders => Folders.Count > 0;

    public ContentListViewModel(ContentListService contentService, MediaRepository mediaRepo, CustomPlatformRepository customPlatformRepo)
    {
        _contentService = contentService;
        _mediaRepo = mediaRepo;
        _customPlatformRepo = customPlatformRepo;
    }

    public async Task LoadPlatformAsync(Platform platform)
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            _currentViewType = PlatformViewType.Standard;
            _currentPlatform = platform;
            _currentCustomPlatformIds = null;
            _currentCustomPlatformId = null;
            _currentFolderId = null;

            var items = await _contentService.GetPlatformItemsAsync(platform);
            var folders = await _contentService.GetPlatformFoldersAsync(platform);

            Items.Clear();
            foreach (var item in items) Items.Add(item);

            Folders.Clear();
            foreach (var folder in folders) Folders.Add(folder);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadFolderAsync(Guid folderId)
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();
        _savedViewType = _currentViewType != PlatformViewType.Folder ? _currentViewType : _savedViewType;
        _currentViewType = PlatformViewType.Folder;
        _currentFolderId = folderId;
        OnPropertyChanged(nameof(IsInFolder));
        OnPropertyChanged(nameof(CurrentFolderId));

        try
        {
            var items = await _contentService.GetFolderItemsAsync(folderId);
            var subfolders = await _contentService.GetChildFoldersAsync(folderId);

            FolderTitle = await _contentService.GetFolderNameAsync(folderId) ?? "文件夹";
            PageTitle = FolderTitle;
            OnPropertyChanged(nameof(FolderTitle));
            OnPropertyChanged(nameof(PageTitle));

            // Fill custom platform names (same as platform pages)
            await FillCustomPlatformNamesAsync(items);

            var imagePaths = await LoadFirstImagePathsAsync(items);

            Items.Clear();
            foreach (var item in items)
            {
                if (imagePaths.TryGetValue(item.Id, out var path))
                    item.FirstImagePath = path;
                Items.Add(item);
            }

            Folders.Clear();
            foreach (var folder in subfolders) Folders.Add(folder);

            OnPropertyChanged(nameof(HasItems));
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadMergedPlatformAsync(Platform platform, List<Guid> customPlatformIds)
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            _currentViewType = PlatformViewType.Merged;
            _currentPlatform = platform;
            _currentCustomPlatformIds = customPlatformIds;
            _currentCustomPlatformId = null;
            _currentFolderId = null;

            var items = await _contentService.GetMergedPlatformItemsAsync(platform, customPlatformIds);
            var folders = await _contentService.GetMergedPlatformFoldersAsync(platform, customPlatformIds);

            await FillCustomPlatformNamesAsync(items);

            var imagePaths = await LoadFirstImagePathsAsync(items);

            Items.Clear();
            foreach (var item in items)
            {
                if (imagePaths.TryGetValue(item.Id, out var path))
                    item.FirstImagePath = path;
                Items.Add(item);
            }

            Folders.Clear();
            foreach (var folder in folders) Folders.Add(folder);

            OnPropertyChanged(nameof(HasItems));
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadCustomPlatformAsync(Guid customPlatformId, string? pageTitle = null)
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            _currentViewType = PlatformViewType.Custom;
            _currentPlatform = null;
            _currentCustomPlatformIds = null;
            _currentCustomPlatformId = customPlatformId;
            _currentFolderId = null;
            if (pageTitle != null) SetPageTitle(pageTitle);
            var items = await _contentService.GetCustomPlatformItemsAsync(customPlatformId);
            var folders = await _contentService.GetCustomPlatformFoldersAsync(customPlatformId);

            // Fill custom platform names
            await FillCustomPlatformNamesAsync(items);

            // Load first image paths
            var imagePaths = await LoadFirstImagePathsAsync(items);

            Items.Clear();
            foreach (var item in items)
            {
                if (imagePaths.TryGetValue(item.Id, out var path))
                    item.FirstImagePath = path;
                Items.Add(item);
            }

            Folders.Clear();
            foreach (var folder in folders) Folders.Add(folder);

            OnPropertyChanged(nameof(HasItems));
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoadUncategorizedAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            _currentViewType = PlatformViewType.Uncategorized;
            _currentPlatform = null;
            _currentCustomPlatformIds = null;
            _currentCustomPlatformId = null;
            _currentFolderId = null;
            var items = await _contentService.GetUncategorizedItemsAsync();
            var folders = await _contentService.GetUncategorizedFoldersAsync();

            // Fill custom platform names
            await FillCustomPlatformNamesAsync(items);

            // Load first image paths
            var imagePaths = await LoadFirstImagePathsAsync(items);

            Items.Clear();
            foreach (var item in items)
            {
                if (imagePaths.TryGetValue(item.Id, out var path))
                    item.FirstImagePath = path;
                Items.Add(item);
            }

            Folders.Clear();
            foreach (var folder in folders) Folders.Add(folder);

            OnPropertyChanged(nameof(HasItems));
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<Dictionary<Guid, string>> LoadFirstImagePathsAsync(List<Item> items)
    {
        var result = new Dictionary<Guid, string>();
        foreach (var item in items)
        {
            var assets = await _mediaRepo.GetByItemIdAsync(item.Id);
            var first = assets.FirstOrDefault(a => a.Type == MediaType.cover || a.Type == MediaType.image);
            if (first?.LocalPath != null)
            {
                var fullPath = MediaPathHelper.ResolveFullPath(first.LocalPath);
                if (File.Exists(fullPath))
                    result[item.Id] = fullPath;
            }
        }
        return result;
    }

    private async Task FillCustomPlatformNamesAsync(List<Item> items)
    {
        var customPlatforms = await _customPlatformRepo.GetAllAsync();
        var platformDict = customPlatforms.ToDictionary(cp => cp.Id, cp => cp.Name);

        foreach (var item in items)
        {
            if (item.Platform == Platform.custom && item.CustomPlatformId != null)
            {
                if (platformDict.TryGetValue(item.CustomPlatformId.Value, out var name))
                    item.CustomPlatformName = name;
            }
        }
    }

    /// <summary>
    /// 返回按钮后清空文件夹状态，恢复到平台页
    /// </summary>
    public void ClearFolderState()
    {
        _currentFolderId = null;
        _currentViewType = _savedViewType;
        FolderTitle = "";
        OnPropertyChanged(nameof(IsInFolder));
        OnPropertyChanged(nameof(CurrentFolderId));
        OnPropertyChanged(nameof(FolderTitle));
        // Force clear items so ReloadCurrentContentAsync repopulates
        Items.Clear();
        Folders.Clear();
        OnPropertyChanged(nameof(HasItems));
    }

    /// <summary>
    /// 重新加载当前查看的内容（删除/导入后调用）
    /// </summary>
    public async Task ReloadCurrentContentAsync()
    {
        switch (_currentViewType)
        {
            case PlatformViewType.Standard when _currentPlatform.HasValue:
                await LoadPlatformAsync(_currentPlatform.Value);
                break;
            case PlatformViewType.Merged when _currentPlatform.HasValue && _currentCustomPlatformIds != null:
                await LoadMergedPlatformAsync(_currentPlatform.Value, _currentCustomPlatformIds);
                break;
            case PlatformViewType.Custom when _currentCustomPlatformId.HasValue:
                await LoadCustomPlatformAsync(_currentCustomPlatformId.Value);
                break;
            case PlatformViewType.Uncategorized:
                await LoadUncategorizedAsync();
                break;
            case PlatformViewType.Folder when _currentFolderId.HasValue:
                await LoadFolderAsync(_currentFolderId.Value);
                break;
        }
    }

    /// <summary>
    /// 移动到平台请求回调（由 MainWindowViewModel 订阅处理）
    /// </summary>
    public Action<Item>? OnMoveToPlatformRequested { get; set; }

    /// <summary>
    /// 新建文件夹请求回调
    /// </summary>
    public Action? OnNewFolderRequested { get; set; }

    /// <summary>
    /// 点击文件夹标签回调
    /// </summary>
    public Action<Folder>? OnFolderClicked { get; set; }

    /// <summary>
    /// 重命名文件夹请求回调
    /// </summary>
    public Action<Folder>? OnFolderRenameRequested { get; set; }

    /// <summary>
    /// 删除文件夹请求回调
    /// </summary>
    public Action<Folder>? OnFolderDeleteRequested { get; set; }

    /// <summary>
    /// 移动到文件夹请求回调
    /// </summary>
    public Action<Item>? OnMoveToFolderRequested { get; set; }

    /// <summary>
    /// 文件夹操作成功后的回调（刷新 Sidebar 计数等）
    /// </summary>
    public Func<Task>? OnFolderChanged { get; set; }

    /// <summary>
    /// 删除内容请求回调
    /// </summary>
    public Action<Item>? OnDeleteItemRequested { get; set; }

    /// <summary>
    /// 返回平台页请求回调
    /// </summary>
    public Action? OnNavigateBackRequested { get; set; }

    /// <summary>
    /// 卡片点击回调（直接导航，不依赖 SelectedItem 变更）
    /// </summary>
    public Action<Item>? OnItemSelected { get; set; }
}
