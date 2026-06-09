using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 搜索 ViewModel — 关键词搜索内容
/// </summary>
public partial class SearchViewModel : ViewModelBase
{
    private readonly SearchService _searchService;
    private readonly MediaRepository _mediaRepo;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private Item? _selectedItem;

    public ObservableCollection<Item> Results { get; } = new();

    public bool HasResults => Results.Count > 0;

    public SearchViewModel(SearchService searchService, MediaRepository mediaRepo)
    {
        _searchService = searchService;
        _mediaRepo = mediaRepo;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (IsBusy) return;

        var trimmed = Query?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            Results.Clear();
            OnPropertyChanged(nameof(HasResults));
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var items = await _searchService.SearchAsync(trimmed);

            // Load first image paths
            var imagePaths = new Dictionary<Guid, string>();
            foreach (var item in items)
            {
                var assets = await _mediaRepo.GetByItemIdAsync(item.Id);
                var first = assets.FirstOrDefault(a => a.Type == MediaType.cover || a.Type == MediaType.image);
                if (first?.LocalPath != null)
                {
                    var fullPath = MediaPathHelper.ResolveFullPath(first.LocalPath);
                    if (File.Exists(fullPath))
                        imagePaths[item.Id] = fullPath;
                }
            }

            Results.Clear();
            foreach (var item in items)
            {
                if (imagePaths.TryGetValue(item.Id, out var path))
                    item.FirstImagePath = path;
                Results.Add(item);
            }
            OnPropertyChanged(nameof(HasResults));
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
