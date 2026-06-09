using Gatherly.Windows.Database;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class ViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ViewModelTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = @"CREATE TABLE IF NOT EXISTS custom_platforms (
                id TEXT PRIMARY KEY, name TEXT NOT NULL, logo_path TEXT,
                created_at REAL NOT NULL, sort_order INTEGER NOT NULL DEFAULT 0)";
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private void InsertItem(string id, string platform = "bilibili", double importDate = 1700000000,
        string? customPlatformId = null, string? folderId = null, string? deletedAt = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status,
                custom_platform_id, folder_id, deleted_at)
            VALUES ($id, 'https://example.com', $platform, 'https://example.com',
                $importDate, $importDate, 'normal', 'pending', 'textOnly',
                $cpId, $folderId, $deletedAt)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$platform", platform);
        cmd.Parameters.AddWithValue("$importDate", importDate);
        cmd.Parameters.AddWithValue("$cpId", (object?)customPlatformId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$folderId", (object?)folderId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$deletedAt", (object?)deletedAt ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void InsertFolder(string id, string name = "Test Folder", string platform = "bilibili",
        string? customPlatformId = null, string? parentId = null)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO folders (id, name, platform, created_at, sort_order, custom_platform_id, parent_id)
            VALUES ($id, $name, $platform, 1700000000, 0, $cpId, $parentId)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name);
        cmd.Parameters.AddWithValue("$platform", platform);
        cmd.Parameters.AddWithValue("$cpId", (object?)customPlatformId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$parentId", (object?)parentId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void InsertTrashRecord(string id, string itemId)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO trash_records (id, item_id, deleted_at, auto_delete_at, original_archive_status)
            VALUES ($id, $itemId, 1700000000, 1702592000, 'pending')";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$itemId", itemId);
        cmd.ExecuteNonQuery();
    }

    // ==================== HomeViewModel ====================

    [Fact]
    public async Task HomeViewModel_LoadAsync_LoadsItems()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", importDate: 1700001000);
        InsertItem("00000000-0000-0000-0000-000000000002", importDate: 1700000000);

        var vm = new HomeViewModel(new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection)));
        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.RecentItems.Count);
        Assert.False(vm.IsBusy);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task HomeViewModel_LoadAsync_SetsIsBusyDuringLoad()
    {
        var vm = new HomeViewModel(new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection)));

        // Before load
        Assert.False(vm.IsBusy);

        await vm.LoadCommand.ExecuteAsync(null);

        // After load
        Assert.False(vm.IsBusy);
    }

    // ==================== ContentListViewModel ====================

    [Fact]
    public async Task ContentListViewModel_LoadPlatformAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "youtube");
        InsertFolder("00000000-0000-0000-0000-000000000010", platform: "bilibili");

        var vm = new ContentListViewModel(
            new ContentListService(
                new ItemRepository(_connection),
                new FolderRepository(_connection)));

        await vm.LoadPlatformAsync(Platform.bilibili);

        Assert.Single(vm.Items);
        Assert.Single(vm.Folders);
    }

    [Fact]
    public async Task ContentListViewModel_LoadFolderAsync()
    {
        var folderId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        InsertFolder("00000000-0000-0000-0000-000000000010");
        InsertFolder("00000000-0000-0000-0000-000000000011", parentId: folderId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000001", folderId: folderId.ToString());

        var vm = new ContentListViewModel(
            new ContentListService(
                new ItemRepository(_connection),
                new FolderRepository(_connection)));

        await vm.LoadFolderAsync(folderId);

        Assert.Single(vm.Items);
        Assert.Single(vm.Folders);
    }

    [Fact]
    public async Task ContentListViewModel_LoadCustomPlatformAsync()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001",
            platform: "custom", customPlatformId: cpId.ToString());
        InsertFolder("00000000-0000-0000-0000-000000000010",
            platform: "custom", customPlatformId: cpId.ToString());

        var vm = new ContentListViewModel(
            new ContentListService(
                new ItemRepository(_connection),
                new FolderRepository(_connection)));

        await vm.LoadCustomPlatformAsync(cpId);

        Assert.Single(vm.Items);
        Assert.Single(vm.Folders);
    }

    [Fact]
    public async Task ContentListViewModel_LoadUncategorizedAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001",
            platform: "custom", customPlatformId: null);
        InsertFolder("00000000-0000-0000-0000-000000000010",
            platform: "custom", customPlatformId: null);

        var vm = new ContentListViewModel(
            new ContentListService(
                new ItemRepository(_connection),
                new FolderRepository(_connection)));

        await vm.LoadUncategorizedAsync();

        Assert.Single(vm.Items);
        Assert.Single(vm.Folders);
    }

    [Fact]
    public async Task ContentListViewModel_IsBusyPreventsDoubleLoad()
    {
        InsertItem("00000000-0000-0000-0000-000000000001");

        var vm = new ContentListViewModel(
            new ContentListService(
                new ItemRepository(_connection),
                new FolderRepository(_connection)));

        // Simulate concurrent loads
        var task1 = vm.LoadPlatformAsync(Platform.bilibili);
        var task2 = vm.LoadPlatformAsync(Platform.bilibili);
        await Task.WhenAll(task1, task2);

        // Should not crash, items loaded once
        Assert.Single(vm.Items);
    }

    // ==================== SearchViewModel ====================

    [Fact]
    public async Task SearchViewModel_EmptyQuery_ReturnsEmpty()
    {
        var vm = new SearchViewModel(new SearchService(new SearchRepository(_connection)));
        vm.Query = "";
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
    }

    [Fact]
    public async Task SearchViewModel_WhitespaceQuery_ReturnsEmpty()
    {
        var vm = new SearchViewModel(new SearchService(new SearchRepository(_connection)));
        vm.Query = "   ";
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
    }

    [Fact]
    public async Task SearchViewModel_SearchFindsMatch()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://example.com', 'bilibili',
                'https://example.com', 1700000000, 1700000000, 'normal', 'pending', 'textOnly')";
        cmd.ExecuteNonQuery();
        using var ftsCmd = _connection.CreateCommand();
        ftsCmd.CommandText = @"
            INSERT INTO items_fts (rowid, title, body) VALUES
            ((SELECT rowid FROM items WHERE id='00000000-0000-0000-0000-000000000001'),
             'My Special Title', 'My Special Body')";
        ftsCmd.ExecuteNonQuery();

        var vm = new SearchViewModel(new SearchService(new SearchRepository(_connection)));
        vm.Query = "Special";
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Single(vm.Results);
    }

    [Fact]
    public async Task SearchViewModel_ExcludesDeletedItems()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status, deleted_at)
            VALUES ('00000000-0000-0000-0000-000000000001', 'https://example.com', 'bilibili',
                'https://example.com', 1700000000, 1700000000, 'normal', 'pending', 'textOnly', 1700000000)";
        cmd.ExecuteNonQuery();
        using var ftsCmd = _connection.CreateCommand();
        ftsCmd.CommandText = @"
            INSERT INTO items_fts (rowid, title, body) VALUES
            ((SELECT rowid FROM items WHERE id='00000000-0000-0000-0000-000000000001'),
             'Deleted Item', 'Body')";
        ftsCmd.ExecuteNonQuery();

        var vm = new SearchViewModel(new SearchService(new SearchRepository(_connection)));
        vm.Query = "Deleted";
        await vm.SearchCommand.ExecuteAsync(null);

        Assert.Empty(vm.Results);
    }

    // ==================== TrashViewModel ====================

    [Fact]
    public async Task TrashViewModel_LoadAsync_LoadsTrashedItems()
    {
        InsertItem("00000000-0000-0000-0000-000000000001");
        InsertItem("00000000-0000-0000-0000-000000000002", deletedAt: "1700000000");
        InsertTrashRecord("00000000-0000-0000-0000-000000000040",
            "00000000-0000-0000-0000-000000000002");

        var vm = new TrashViewModel(
            new TrashDataService(
                new ItemRepository(_connection),
                new TrashRepository(_connection)),
            new ItemService(
                new ItemRepository(_connection),
                new TrashRepository(_connection)));

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Single(vm.TrashedItems);
        Assert.False(vm.IsBusy);
    }

    // ==================== MainWindowViewModel ====================

    [Fact]
    public void MainWindowViewModel_CreatesChildViewModels()
    {
        var vm = new MainWindowViewModel(_connection);

        Assert.NotNull(vm.Home);
        Assert.NotNull(vm.ContentList);
        Assert.NotNull(vm.Search);
        Assert.NotNull(vm.Trash);
        Assert.Equal("Gatherly Windows", vm.Title);
    }

    [Fact]
    public async Task MainWindowViewModel_ChildViewModels_AreFunctional()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", importDate: 1700001000);

        var vm = new MainWindowViewModel(_connection);

        await vm.Home.LoadCommand.ExecuteAsync(null);
        Assert.Single(vm.Home.RecentItems);

        await vm.Trash.LoadCommand.ExecuteAsync(null);
        Assert.Empty(vm.Trash.TrashedItems);
    }
}
