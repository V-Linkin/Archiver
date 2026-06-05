using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gatherly.Windows.Database;
using Gatherly.Windows.Services;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 主窗口 ViewModel — 根容器，持有子 ViewModel 和最小导航状态
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Gatherly Windows";

    [ObservableProperty]
    private string _currentSection = "Home";

    public HomeViewModel Home { get; }
    public ContentListViewModel ContentList { get; }
    public SearchViewModel Search { get; }
    public TrashViewModel Trash { get; }

    public MainWindowViewModel(SqliteConnection connection)
    {
        var itemRepo = new ItemRepository(connection);
        var folderRepo = new FolderRepository(connection);
        var searchRepo = new SearchRepository(connection);
        var trashRepo = new TrashRepository(connection);

        Home = new HomeViewModel(new HomeDataService(itemRepo));
        ContentList = new ContentListViewModel(new ContentListService(itemRepo, folderRepo));
        Search = new SearchViewModel(new SearchService(searchRepo));
        Trash = new TrashViewModel(new TrashDataService(itemRepo, trashRepo));

        // Load home data on startup
        _ = Home.LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private void ShowHome() => CurrentSection = "Home";

    [RelayCommand]
    private void ShowSearch() => CurrentSection = "Search";

    [RelayCommand]
    private void ShowTrash() => CurrentSection = "Trash";
}
