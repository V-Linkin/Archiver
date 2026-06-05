using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Models;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 搜索 ViewModel — 关键词搜索内容
/// </summary>
public partial class SearchViewModel : ViewModelBase
{
    private readonly SearchService _searchService;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private Item? _selectedItem;

    public ObservableCollection<Item> Results { get; } = new();

    public SearchViewModel(SearchService searchService)
    {
        _searchService = searchService;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (IsBusy) return;

        var trimmed = Query?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            Results.Clear();
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var items = await _searchService.SearchAsync(trimmed);
            Results.Clear();
            foreach (var item in items)
            {
                Results.Add(item);
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
