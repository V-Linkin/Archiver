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

    public bool HasSelectedTrashItem => SelectedItem != null;

    /// <summary>
    /// 回收站操作成功后的回调（由 MainWindowViewModel 订阅以刷新 Sidebar）
    /// </summary>
    public Func<Task>? OnTrashOperationSuccess { get; set; }

    partial void OnSelectedItemChanged(Item? value)
    {
        OnPropertyChanged(nameof(HasSelectedTrashItem));
    }

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

            // Load trash records for remaining days
            TrashRecords.Clear();
            foreach (var item in items)
            {
                var record = await _trashService.GetTrashRecordAsync(item.Id);
                if (record != null)
                    TrashRecords[item.Id] = record;
            }
            OnPropertyChanged(nameof(TrashRecords));
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

    /// <summary>
    /// item ID → TrashRecord 映射，用于显示剩余天数
    /// </summary>
    public Dictionary<Guid, TrashRecord> TrashRecords { get; } = new();

    /// <summary>
    /// 获取指定 item 的剩余天数显示文本
    /// </summary>
    public string GetRemainingDaysText(Guid itemId)
    {
        if (!TrashRecords.TryGetValue(itemId, out var record))
            return "";

        var remaining = record.AutoDeleteAt - DateTimeOffset.UtcNow;
        var days = (int)remaining.TotalDays;
        if (days < 0) days = 0;
        return $"剩余 {days} 天";
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

            if (OnTrashOperationSuccess != null)
                await OnTrashOperationSuccess();
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

            if (OnTrashOperationSuccess != null)
                await OnTrashOperationSuccess();
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
    }
}
