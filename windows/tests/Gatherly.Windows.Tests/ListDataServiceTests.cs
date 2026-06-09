using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class ListDataServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ListDataServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
        // custom_platforms 表需手动建
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

    // ==================== HomeDataService ====================

    [Fact]
    public async Task HomeDataService_GetRecentItemsAsync_ReturnsOrdered()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", importDate: 1700000000);
        InsertItem("00000000-0000-0000-0000-000000000002", importDate: 1700001000);
        InsertItem("00000000-0000-0000-0000-000000000003", importDate: 1700002000);

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var items = await service.GetRecentItemsAsync();

        Assert.Equal(3, items.Count);
        Assert.Equal("00000000-0000-0000-0000-000000000003", items[0].Id.ToString());
        Assert.Equal("00000000-0000-0000-0000-000000000002", items[1].Id.ToString());
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[2].Id.ToString());
    }

    [Fact]
    public async Task HomeDataService_GetRecentItemsAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
            InsertItem($"00000000-0000-0000-0000-{i:D12}", importDate: 1700000000 + i);

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var items = await service.GetRecentItemsAsync(limit: 3);

        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task HomeDataService_GetRecentItemsAsync_ExcludesDeleted()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", importDate: 1700000000);
        InsertItem("00000000-0000-0000-0000-000000000002", importDate: 1700001000, deletedAt: "1700001000");

        var service = new HomeDataService(new ItemRepository(_connection), new MediaRepository(_connection), new CustomPlatformRepository(_connection), _connection);
        var items = await service.GetRecentItemsAsync();

        Assert.Single(items);
    }

    // ==================== ContentListService ====================

    [Fact]
    public async Task ContentListService_GetPlatformItemsAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", platform: "bilibili");
        InsertItem("00000000-0000-0000-0000-000000000002", platform: "youtube");

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var bilibiliItems = await service.GetPlatformItemsAsync(Platform.bilibili);
        var youtubeItems = await service.GetPlatformItemsAsync(Platform.youtube);

        Assert.Single(bilibiliItems);
        Assert.Single(youtubeItems);
    }

    [Fact]
    public async Task ContentListService_GetPlatformFoldersAsync()
    {
        InsertFolder("00000000-0000-0000-0000-000000000010", "Folder A", platform: "bilibili");
        InsertFolder("00000000-0000-0000-0000-000000000011", "Folder B", platform: "youtube");

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var bilibiliFolders = await service.GetPlatformFoldersAsync(Platform.bilibili);

        Assert.Single(bilibiliFolders);
        Assert.Equal("Folder A", bilibiliFolders[0].Name);
    }

    [Fact]
    public async Task ContentListService_GetFolderItemsAsync()
    {
        var folderId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        InsertFolder("00000000-0000-0000-0000-000000000010");
        InsertItem("00000000-0000-0000-0000-000000000001", folderId: folderId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002", folderId: null);

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var items = await service.GetFolderItemsAsync(folderId);

        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[0].Id.ToString());
    }

    [Fact]
    public async Task ContentListService_GetChildFoldersAsync()
    {
        var parentId = Guid.Parse("00000000-0000-0000-0000-000000000010");
        InsertFolder("00000000-0000-0000-0000-000000000010");
        InsertFolder("00000000-0000-0000-0000-000000000011", parentId: parentId.ToString());
        InsertFolder("00000000-0000-0000-0000-000000000012"); // no parent

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var subfolders = await service.GetChildFoldersAsync(parentId);

        Assert.Single(subfolders);
        Assert.Equal("00000000-0000-0000-0000-000000000011", subfolders[0].Id.ToString());
    }

    [Fact]
    public async Task ContentListService_GetCustomPlatformItemsAsync()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000001",
            platform: "custom", customPlatformId: cpId.ToString());
        InsertItem("00000000-0000-0000-0000-000000000002",
            platform: "custom", customPlatformId: null);

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var items = await service.GetCustomPlatformItemsAsync(cpId);

        Assert.Single(items);
    }

    [Fact]
    public async Task ContentListService_GetCustomPlatformFoldersAsync()
    {
        var cpId = Guid.Parse("00000000-0000-0000-0000-000000000030");
        InsertFolder("00000000-0000-0000-0000-000000000010",
            platform: "custom", customPlatformId: cpId.ToString());
        InsertFolder("00000000-0000-0000-0000-000000000011",
            platform: "custom", customPlatformId: null);

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var folders = await service.GetCustomPlatformFoldersAsync(cpId);

        Assert.Single(folders);
    }

    [Fact]
    public async Task ContentListService_GetUncategorizedItemsAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001",
            platform: "custom", customPlatformId: null);
        InsertItem("00000000-0000-0000-0000-000000000002",
            platform: "custom", customPlatformId: "00000000-0000-0000-0000-000000000030");
        InsertItem("00000000-0000-0000-0000-000000000003",
            platform: "bilibili");

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var items = await service.GetUncategorizedItemsAsync();

        Assert.Single(items);
        Assert.Equal("00000000-0000-0000-0000-000000000001", items[0].Id.ToString());
    }

    [Fact]
    public async Task ContentListService_GetUncategorizedFoldersAsync()
    {
        InsertFolder("00000000-0000-0000-0000-000000000010",
            platform: "custom", customPlatformId: null);
        InsertFolder("00000000-0000-0000-0000-000000000011",
            platform: "custom", customPlatformId: "00000000-0000-0000-0000-000000000030");

        var service = new ContentListService(
            new ItemRepository(_connection),
            new FolderRepository(_connection));

        var folders = await service.GetUncategorizedFoldersAsync();

        Assert.Single(folders);
    }

    // ==================== TrashDataService ====================

    [Fact]
    public async Task TrashDataService_GetTrashedItemsAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001");
        InsertItem("00000000-0000-0000-0000-000000000002", deletedAt: "1700000000");

        var service = new TrashDataService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var trashed = await service.GetTrashedItemsAsync();

        Assert.Single(trashed);
        Assert.Equal("00000000-0000-0000-0000-000000000002", trashed[0].Id.ToString());
    }

    [Fact]
    public async Task TrashDataService_GetTrashRecordAsync()
    {
        InsertItem("00000000-0000-0000-0000-000000000001", deletedAt: "1700000000");
        InsertTrashRecord("00000000-0000-0000-0000-000000000040",
            "00000000-0000-0000-0000-000000000001");

        var service = new TrashDataService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var record = await service.GetTrashRecordAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));

        Assert.NotNull(record);
        Assert.Equal("00000000-0000-0000-0000-000000000040", record!.Id.ToString());
    }

    [Fact]
    public async Task TrashDataService_GetTrashRecordAsync_NotFound()
    {
        var service = new TrashDataService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var record = await service.GetTrashRecordAsync(Guid.NewGuid());

        Assert.Null(record);
    }

    // ==================== SearchService ====================

    [Fact]
    public async Task SearchService_SearchAsync_EmptyQuery()
    {
        var service = new SearchService(new SearchRepository(_connection));
        var results = await service.SearchAsync("");
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchService_SearchAsync_FindsMatch()
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

        var service = new SearchService(new SearchRepository(_connection));
        var results = await service.SearchAsync("Special");

        Assert.NotEmpty(results);
    }
}
