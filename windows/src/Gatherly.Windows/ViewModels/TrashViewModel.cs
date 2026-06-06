using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 回收站 ViewModel — 已删除内容列表 + 恢复 / 永久删除
/// </summary>
public partial class TrashViewModel : ViewModelBase
{
    private readonly TrashDataService _trashService;
    private readonly ItemService _itemService;

    public ObservableCollection<Item> TrashedItems { get; } = new();

    [ObservableProperty]
    private Item? _selectedItem;

    public TrashViewModel(TrashDataService trashService, ItemService itemService)
    {
        _trashService = trashService;
        _itemService = itemService;
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

    [RelayCommand]
    private async Task RestoreSelectedItemAsync()
    {
        if (SelectedItem == null) return;

        try
        {
            await _itemService.RestoreItemAsync(SelectedItem);
            SelectedItem = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }

    [RelayCommand]
    private async Task PermanentlyDeleteSelectedItemAsync()
    {
        if (SelectedItem == null) return;

        try
        {
            await _itemService.PermanentlyDeleteItemAsync(SelectedItem);
            SelectedItem = null;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }
}
