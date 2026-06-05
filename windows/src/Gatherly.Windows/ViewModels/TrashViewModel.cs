using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 回收站 ViewModel — 已删除内容列表（只读）
/// </summary>
public partial class TrashViewModel : ViewModelBase
{
    private readonly TrashDataService _trashService;

    public ObservableCollection<Item> TrashedItems { get; } = new();

    public TrashViewModel(TrashDataService trashService)
    {
        _trashService = trashService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ClearError();

        try
        {
            var items = await _trashService.GetTrashedItemsAsync();
            TrashedItems.Clear();
            foreach (var item in items)
            {
                TrashedItems.Add(item);
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
