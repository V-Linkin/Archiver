using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 首页 ViewModel — 最近导入内容列表 + 首图 + 平台入口 + 粘贴链接导入
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly HomeDataService _homeService;
    private readonly ImportService _importService;

    public ObservableCollection<Item> RecentItems { get; } = new();
    public ObservableCollection<PlatformEntryDisplay> PlatformEntries { get; } = new();

    [ObservableProperty]
    private Item? _selectedItem;

    [ObservableProperty]
    private string _importUrl = "";

    [ObservableProperty]
    private string? _importStatusMessage;

    [ObservableProperty]
    private bool _isImporting;

    public bool HasItems => RecentItems.Count > 0;
    public bool HasPlatforms => PlatformEntries.Count > 0;

    public HomeViewModel(HomeDataService homeService, ImportService importService)
    {
        _homeService = homeService;
        _importService = importService;
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
                await LoadAsync();
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
}
