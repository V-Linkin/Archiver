using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 首页 ViewModel — 最近导入内容列表
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly HomeDataService _homeService;

    public ObservableCollection<Item> RecentItems { get; } = new();

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
