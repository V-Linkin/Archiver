using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 首页 ViewModel — 最近导入内容列表 + 首图 + 平台入口 + 粘贴链接导入 + 内嵌搜索
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly HomeDataService _homeService;
    private readonly ImportService _importService;
    private readonly SearchRepository _searchRepo;
    private readonly MediaRepository _mediaRepo;
    private readonly CustomPlatformRepository _customPlatformRepo;

    public ObservableCollection<Item> RecentItems { get; } = new();
    public ObservableCollection<PlatformEntryDisplay> PlatformEntries { get; } = new();
    public ObservableCollection<Item> SearchResults { get; } = new();

    [ObservableProperty]
    private Item? _selectedItem;

    [ObservableProperty]
    private string _importUrl = "";

    [ObservableProperty]
    private string? _importStatusMessage;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private string _searchQuery = "";

    [ObservableProperty]
    private bool _isSearching;

    public bool HasSearchResults => SearchResults.Count > 0;
    public bool ShowHomeContent => string.IsNullOrWhiteSpace(SearchQuery);
    public bool ShowSearchResults => !string.IsNullOrWhiteSpace(SearchQuery);
    public int SearchResultCount => SearchResults.Count;

    /// <summary>
    /// 导入成功后的回调（由 MainWindowViewModel 订阅以刷新 Sidebar）
    /// </summary>
    public Func<Task>? OnImportSuccess { get; set; }

    /// <summary>
    /// 首页平台入口点击回调
    /// </summary>
    public Action<PlatformEntryDisplay>? OnPlatformEntryClicked { get; set; }

    /// <summary>
    /// 首页不显示该内容回调
    /// </summary>
    public Action<Item>? OnHideItemRequested { get; set; }

    /// <summary>
    /// 首页删除内容回调
    /// </summary>
    public Action<Item>? OnDeleteItemRequested { get; set; }

    /// <summary>
    /// 搜索结果移动到平台回调
    /// </summary>
    public Action<Item>? OnMoveToPlatformRequested { get; set; }

    /// <summary>
    /// 搜索结果移动到文件夹回调
    /// </summary>
    public Action<Item>? OnMoveToFolderRequested { get; set; }

    /// <summary>
    /// 首页卡片点击回调（直接导航，不依赖 SelectedItem 变更）
    /// </summary>
    public Action<Item>? OnItemSelected { get; set; }

    public bool HasItems => RecentItems.Count > 0;
    public bool HasPlatforms => PlatformEntries.Count > 0;

    public void NotifyPlatformEntryClicked(PlatformEntryDisplay entry)
    {
        OnPlatformEntryClicked?.Invoke(entry);
    }

    public HomeViewModel(HomeDataService homeService, ImportService importService,
        SearchRepository searchRepo, MediaRepository mediaRepo, CustomPlatformRepository customPlatformRepo)
    {
        _homeService = homeService;
        _importService = importService;
        _searchRepo = searchRepo;
        _mediaRepo = mediaRepo;
        _customPlatformRepo = customPlatformRepo;
    }

    [RelayCommand]
    private async Task ImportLinkAsync()
    {
        if (IsImporting) return;

        IsImporting = true;
        try
        {
            var result = await _importService.ProcessImportAsync(ImportUrl);
            ImportStatusMessage = result.Message;

            if (result.Status != Services.Import.ImportStatus.EmptyInput)
                ImportUrl = "";

            // 导入成功或 DuplicateExistingItem 时刷新首页列表
            if (result.Status == Services.Import.ImportStatus.SuccessImport
                || result.Status == Services.Import.ImportStatus.DuplicateExistingItem)
            {
                await LoadAsync();

                // 通知 MainWindowViewModel 刷新 Sidebar
                if (OnImportSuccess != null)
                    await OnImportSuccess();
            }
        }
        catch (Exception ex)
        {
            ImportStatusMessage = $"导入失败：{ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    /// <summary>
    /// 获取平台统计数据（供 MainWindowViewModel 的 Sidebar 使用）
    /// </summary>
    public async Task<List<PlatformEntryDisplay>> GetPlatformStatsAsync()
    {
        return await _homeService.GetPlatformStatsAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            var items = await _homeService.GetRecentItemsAsync();

            // 过滤已隐藏的 item
            var hiddenFile = Path.Combine(
                Gatherly.Windows.Database.DatabasePaths.DataDirectory, "hidden_items.txt");
            HashSet<string> hiddenIds = new();
            try
            {
                if (File.Exists(hiddenFile))
                    hiddenIds = new HashSet<string>(File.ReadAllLines(hiddenFile));
            }
            catch { }

            items = items.Where(i => !hiddenIds.Contains(i.Id.ToString("D"))).ToList();

            // 批量填充自定义平台名称
            await _homeService.FillCustomPlatformNamesAsync(items);

            // 批量加载首图路径，先设置再添加到集合
            var imagePaths = await _homeService.GetFirstImagePathsAsync(items.Select(i => i.Id));
            foreach (var item in items)
            {
                if (imagePaths.TryGetValue(item.Id, out var path))
                    item.FirstImagePath = path;
            }

            RecentItems.Clear();
            foreach (var item in items)
            {
                RecentItems.Add(item);
            }
            OnPropertyChanged(nameof(HasItems));

            // 加载平台入口
            var platforms = await _homeService.GetPlatformStatsAsync();
            PlatformEntries.Clear();
            foreach (var p in platforms)
            {
                PlatformEntries.Add(p);
            }
            OnPropertyChanged(nameof(HasPlatforms));
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

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(ShowHomeContent));
        OnPropertyChanged(nameof(ShowSearchResults));
        _ = ExecuteSearchAsync();
    }

    private async Task ExecuteSearchAsync()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(query))
        {
            SearchResults.Clear();
            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(SearchResultCount));
            return;
        }

        try
        {
            IsSearching = true;
            var items = await _searchRepo.SearchAsync(query);

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

            SearchResults.Clear();
            foreach (var item in items)
            {
                var assets = await _mediaRepo.GetByItemIdAsync(item.Id);
                var first = assets.FirstOrDefault(a => a.Type == MediaType.cover || a.Type == MediaType.image);
                if (first?.LocalPath != null)
                {
                    var fullPath = MediaPathHelper.ResolveFullPath(first.LocalPath);
                    if (File.Exists(fullPath))
                        item.FirstImagePath = fullPath;
                }
                SearchResults.Add(item);
            }
            OnPropertyChanged(nameof(HasSearchResults));
            OnPropertyChanged(nameof(SearchResultCount));
        }
        catch { }
        finally
        {
            IsSearching = false;
        }
    }
}
