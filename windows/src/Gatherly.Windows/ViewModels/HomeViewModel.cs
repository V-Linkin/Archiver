using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 首页 ViewModel — 最近导入内容列表 + 首图
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly HomeDataService _homeService;

    public ObservableCollection<Item> RecentItems { get; } = new();

    [ObservableProperty]
    private Item? _selectedItem;

    public HomeViewModel(HomeDataService homeService)
    {
        _homeService = homeService;
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
