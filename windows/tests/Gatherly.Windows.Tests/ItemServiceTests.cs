using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class ItemServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ItemServiceTests()
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

    private void InsertTestItem(string id = "00000000-0000-0000-0000-000000000001",
        string? folderId = null, string archiveStatus = "pending")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO items (id, original_url, platform, normalized_url,
                import_date, modify_date, content_status, archive_status, media_status, folder_id)
            VALUES ($id, 'https://example.com', 'bilibili', 'https://example.com',
                1700000000, 1700000000, 'normal', $archiveStatus, 'textOnly', $folderId)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$archiveStatus", archiveStatus);
        cmd.Parameters.AddWithValue("$folderId", (object?)folderId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    // ==================== ItemService.TrashItemAsync ====================

    [Fact]
    public async Task TrashItemAsync_SetsContentStatusToTrashed()
    {
        InsertTestItem();
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.TrashItemAsync(item);

        var updated = await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Equal(ContentStatus.trashed, updated!.ContentStatus);
    }

    [Fact]
    public async Task TrashItemAsync_SetsDeletedAt()
    {
        InsertTestItem();
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.TrashItemAsync(item);

        var updated = await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(updated!.DeletedAt);
    }

    [Fact]
    public async Task TrashItemAsync_CreatesTrashRecord()
    {
        InsertTestItem();
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.TrashItemAsync(item);

        var record = await new TrashRepository(_connection).GetByItemIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(record);
    }

    [Fact]
    public async Task TrashItemAsync_SavesOriginalFolderId()
    {
        var folderId = Guid.NewGuid();
        InsertTestItem(folderId: folderId.ToString());
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.TrashItemAsync(item);

        var record = await new TrashRepository(_connection).GetByItemIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Equal(folderId, record!.OriginalFolderId);
    }

    [Fact]
    public async Task TrashItemAsync_SavesOriginalArchiveStatus()
    {
        InsertTestItem(archiveStatus: "archived");
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.TrashItemAsync(item);

        var record = await new TrashRepository(_connection).GetByItemIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Equal(ArchiveStatus.archived, record!.OriginalArchiveStatus);
    }

    [Fact]
    public async Task TrashItemAsync_SavesMediaPaths()
    {
        InsertTestItem();
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        var mediaPaths = new List<string> { "img1.jpg", "video1.mp4" };
        await service.TrashItemAsync(item, mediaPaths);

        var record = await new TrashRepository(_connection).GetByItemIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Equal(2, record!.MediaPaths.Count);
        Assert.Equal("img1.jpg", record.MediaPaths[0]);
    }

    [Fact]
    public async Task TrashItemAsync_AutoDeleteAtIs30DaysLater()
    {
        InsertTestItem();
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.TrashItemAsync(item);

        var record = await new TrashRepository(_connection).GetByItemIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var diff = record!.AutoDeleteAt - record.DeletedAt;
        Assert.Equal(30, diff.Days);
    }

    [Fact]
    public async Task TrashItemAsync_GetByIdStillReadable()
    {
        InsertTestItem();
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.TrashItemAsync(item);

        var repo = new ItemRepository(_connection);
        var byId = await repo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(byId);
        Assert.Equal(ContentStatus.trashed, byId!.ContentStatus);
    }

    [Fact]
    public async Task TrashItemAsync_GetTrashedAsync_ReadsItem()
    {
        InsertTestItem();
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.TrashItemAsync(item);

        var trashed = await new ItemRepository(_connection).GetTrashedAsync();
        Assert.Single(trashed);
        Assert.Equal("00000000-0000-0000-0000-000000000001", trashed[0].Id.ToString());
    }

    [Fact]
    public async Task TrashItemAsync_ExcludedFromNormalLists()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        InsertTestItem("00000000-0000-0000-0000-000000000002");
        var service = new ItemService(
            new ItemRepository(_connection),
            new TrashRepository(_connection));

        var item1 = (await new ItemRepository(_connection).GetByIdAsync(
            Guid.Parse("00000000-0000-0000-0000-000000000001")))!;
        await service.TrashItemAsync(item1);

        var recent = await new ItemRepository(_connection).GetRecentAsync();
        Assert.Single(recent);
        Assert.Equal("00000000-0000-0000-0000-000000000002", recent[0].Id.ToString());
    }

    // ==================== MainWindowViewModel.TrashSelectedItemCommand ====================

    [Fact]
    public async Task MainWindowViewModel_TrashSelectedItemCommand_NullDoesNotCrash()
    {
        var vm = new MainWindowViewModel(_connection);
        vm.SelectedItem = null;
        await vm.TrashSelectedItemCommand.ExecuteAsync(null);
        Assert.Null(vm.SelectedItem);
    }

    [Fact]
    public async Task MainWindowViewModel_TrashSelectedItemCommand_TrashesItem()
    {
        InsertTestItem();
        var vm = new MainWindowViewModel(_connection);

        // Wait for home to load
        await vm.Home.LoadCommand.ExecuteAsync(null);

        // Select an item
        var item = vm.Home.RecentItems.FirstOrDefault();
        if (item != null)
        {
            vm.SelectedItem = item;
            await vm.TrashSelectedItemCommand.ExecuteAsync(null);

            // Selection should be cleared
            Assert.Null(vm.SelectedItem);
        }
    }

    [Fact]
    public async Task MainWindowViewModel_TrashSelectedItemCommand_RefreshesLists()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        InsertTestItem("00000000-0000-0000-0000-000000000002");
        var vm = new MainWindowViewModel(_connection);

        await vm.Home.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Home.RecentItems.Count);

        var item = vm.Home.RecentItems.First();
        vm.SelectedItem = item;
        await vm.TrashSelectedItemCommand.ExecuteAsync(null);

        // Home should refresh (1 item left)
        Assert.Single(vm.Home.RecentItems);

        // Trash should have 1 item
        await vm.Trash.LoadCommand.ExecuteAsync(null);
        Assert.Single(vm.Trash.TrashedItems);
    }
}
