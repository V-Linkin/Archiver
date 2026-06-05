using CommunityToolkit.Mvvm.ComponentModel;
using Gatherly.Windows.Database;
using Gatherly.Windows.Services;
using Microsoft.Data.Sqlite;

namespace Gatherly.Windows.ViewModels;

/// <summary>
/// 主窗口 ViewModel — 根容器，持有子 ViewModel 引用
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Gatherly Windows";

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
    }
}
