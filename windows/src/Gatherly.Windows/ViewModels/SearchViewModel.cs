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
    private readonly CustomPlatformRepository _customPlatformRepo;

    private CancellationTokenSource? _searchCts;
    private int _searchGeneration;

    [ObservableProperty]
    private string _query = string.Empty;

    [ObservableProperty]
    private Item? _selectedItem;

    /// <summary>
    /// 是否至少执行过一次搜索
    /// </summary>
    private bool _hasSearched;

    public ObservableCollection<Item> Results { get; } = new();

    public bool HasResults => Results.Count > 0;
    public int ResultCount => Results.Count;

    /// <summary>
    /// 是否显示无结果提示（有搜索词 + 已搜索 + 无结果）
    /// </summary>
    public bool ShowNoResults => _hasSearched && !string.IsNullOrWhiteSpace(Query) && Results.Count == 0 && !IsBusy;

    /// <summary>
    /// 是否显示结果计数（有搜索词 + 已搜索 + 有结果）
    /// </summary>
    public bool ShowResultCount => _hasSearched && !string.IsNullOrWhiteSpace(Query) && Results.Count > 0;

    public Action<Item>? OnItemSelected { get; set; }
    public Action<Item>? OnMoveToPlatformRequested { get; set; }
    public Action<Item>? OnMoveToFolderRequested { get; set; }
    public Action<Item>? OnDeleteItemRequested { get; set; }

    public SearchViewModel(SearchService searchService, MediaRepository mediaRepo, CustomPlatformRepository customPlatformRepo)
    {
        _searchService = searchService;
        _mediaRepo = mediaRepo;
        _customPlatformRepo = customPlatformRepo;
    }

    partial void OnQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _hasSearched = false;
            Results.Clear();
            NotifySearchState();
        }
    }

    public void Reset()
    {
        _searchGeneration++;
        _searchCts?.Cancel();
        _searchCts = null;
        Query = string.Empty;
        Results.Clear();
        SelectedItem = null;
        _hasSearched = false;
        ClearError();
        IsBusy = false;
        NotifySearchState();
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (IsBusy) return;

        var trimmed = Query?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
        {
            Results.Clear();
            _hasSearched = false;
            NotifySearchState();
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var currentGeneration = ++_searchGeneration;

        IsBusy = true;
        _hasSearched = true;
        ClearError();
        NotifySearchState();

        try
        {
            var items = await _searchService.SearchAsync(trimmed);

            if (currentGeneration != _searchGeneration)
                return;

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
            NotifySearchState();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            NotifySearchState();
        }
    }

    private void NotifySearchState()
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(ResultCount));
        OnPropertyChanged(nameof(ShowNoResults));
        OnPropertyChanged(nameof(ShowResultCount));
    }
}
