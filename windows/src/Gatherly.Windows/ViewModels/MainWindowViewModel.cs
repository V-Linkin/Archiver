using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Services;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 主窗口 ViewModel — 根容器，持有子 ViewModel、导航状态、选中项和备注编辑
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Gatherly Windows";

    [ObservableProperty]
    private string _currentSection = "Home";

    [ObservableProperty]
    private Item? _selectedItem;

    [ObservableProperty]
    private string _editableRemark = string.Empty;

    [ObservableProperty]
    private bool _isEditingRemark;

    partial void OnSelectedItemChanged(Item? value)
    {
        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(DisplayAuthor));
        OnPropertyChanged(nameof(DisplayBody));
        OnPropertyChanged(nameof(DisplayPlatform));
        OnPropertyChanged(nameof(DisplayPublishDate));
        OnPropertyChanged(nameof(DisplayImportDate));
        OnPropertyChanged(nameof(DisplayRemark));
        OnPropertyChanged(nameof(DisplayOriginalUrl));
        OnPropertyChanged(nameof(DisplayNormalizedUrl));

        // Sync editable remark
        EditableRemark = value?.Remark ?? string.Empty;
        IsEditingRemark = false;
    }

    public bool HasSelectedItem => SelectedItem != null;

    public string DisplayTitle => SelectedItem?.Title ?? "未命名内容";
    public string DisplayAuthor => SelectedItem?.Author ?? "未知作者";
    public string DisplayBody => SelectedItem?.Body ?? "";
    public string DisplayPlatform => SelectedItem?.Platform.ToString() ?? "";
    public string DisplayPublishDate => SelectedItem?.PublishDate?.ToString("yyyy-MM-dd HH:mm") ?? "未知";
    public string DisplayImportDate => SelectedItem?.ImportDate.ToString("yyyy-MM-dd HH:mm") ?? "";
    public string DisplayRemark => SelectedItem?.Remark ?? "";
    public string DisplayOriginalUrl => SelectedItem?.OriginalUrl ?? "";
    public string DisplayNormalizedUrl => SelectedItem?.NormalizedUrl ?? "";

    public HomeViewModel Home { get; }
    public ContentListViewModel ContentList { get; }
    public SearchViewModel Search { get; }
    public TrashViewModel Trash { get; }

    private readonly ItemService _itemService;

    public MainWindowViewModel(SqliteConnection connection)
    {
        var itemRepo = new ItemRepository(connection);
        var folderRepo = new FolderRepository(connection);
        var searchRepo = new SearchRepository(connection);
        var trashRepo = new TrashRepository(connection);

        _itemService = new ItemService(itemRepo, trashRepo);

        Home = new HomeViewModel(new HomeDataService(itemRepo));
        ContentList = new ContentListViewModel(new ContentListService(itemRepo, folderRepo));
        Search = new SearchViewModel(new SearchService(searchRepo));
        Trash = new TrashViewModel(new TrashDataService(itemRepo, trashRepo), _itemService);

        // Subscribe to sub-ViewModel selection changes
        Home.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.SelectedItem))
                SelectedItem = Home.SelectedItem;
        };
        ContentList.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ContentListViewModel.SelectedItem))
                SelectedItem = ContentList.SelectedItem;
        };
        Search.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SearchViewModel.SelectedItem))
                SelectedItem = Search.SelectedItem;
        };
        Trash.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrashViewModel.SelectedItem))
                SelectedItem = Trash.SelectedItem;
        };

        // Load home data on startup
        _ = Home.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowHome() => CurrentSection = "Home";

    [RelayCommand]
    private void ShowSearch() => CurrentSection = "Search";

    [RelayCommand]
    private void ShowTrash() => CurrentSection = "Trash";

    [RelayCommand]
    private async Task TrashSelectedItemAsync()
    {
        if (SelectedItem == null) return;

        try
        {
            await _itemService.TrashItemAsync(SelectedItem);

            SelectedItem = null;
            Home.SelectedItem = null;
            ContentList.SelectedItem = null;
            Search.SelectedItem = null;
            Trash.SelectedItem = null;

            await Home.LoadCommand.ExecuteAsync(null);
            await Trash.LoadCommand.ExecuteAsync(null);
        }
        catch
        {
        }
    }

    [RelayCommand]
    private void StartEditRemark()
    {
        if (SelectedItem == null) return;
        EditableRemark = SelectedItem.Remark ?? string.Empty;
        IsEditingRemark = true;
    }

    [RelayCommand]
    private void CancelEditRemark()
    {
        EditableRemark = SelectedItem?.Remark ?? string.Empty;
        IsEditingRemark = false;
    }

    [RelayCommand]
    private async Task SaveRemarkAsync()
    {
        if (SelectedItem == null) return;

        try
        {
            var updated = await _itemService.UpdateRemarkAsync(SelectedItem, EditableRemark);
            SelectedItem = updated;
            IsEditingRemark = false;
        }
        catch
        {
        }
    }
}
