using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Gatherly.Windows.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class TrashViewModelTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public TrashViewModelTests()
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

    private void InsertTestItem(string id, string? folderId = null, string archiveStatus = "pending")
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

    private async Task TrashItem(string id)
    {
        var itemRepo = new ItemRepository(_connection);
        var trashRepo = new TrashRepository(_connection);
        var service = new ItemService(itemRepo, trashRepo);
        var item = (await itemRepo.GetByIdAsync(Guid.Parse(id)))!;
        await service.TrashItemAsync(item);
    }

    private TrashViewModel CreateViewModel()
    {
        return new TrashViewModel(
            new TrashDataService(
                new ItemRepository(_connection),
                new TrashRepository(_connection)),
            new ItemService(
                new ItemRepository(_connection),
                new TrashRepository(_connection)));
    }

    // ==================== RestoreItemAsync ====================

    [Fact]
    public async Task RestoreItemAsync_ClearsDeletedAt()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var itemRepo = new ItemRepository(_connection);
        var service = new ItemService(itemRepo, new TrashRepository(_connection));
        var item = (await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.RestoreItemAsync(item);

        var restored = await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.NotNull(restored);
        Assert.Null(restored!.DeletedAt);
    }

    [Fact]
    public async Task RestoreItemAsync_RestoresContentStatusToNormal()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var itemRepo = new ItemRepository(_connection);
        var service = new ItemService(itemRepo, new TrashRepository(_connection));
        var item = (await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.RestoreItemAsync(item);

        var restored = await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Equal(ContentStatus.normal, restored!.ContentStatus);
    }

    [Fact]
    public async Task RestoreItemAsync_RestoresOriginalFolderId()
    {
        var folderId = Guid.NewGuid();
        InsertTestItem("00000000-0000-0000-0000-000000000001", folderId: folderId.ToString());
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var itemRepo = new ItemRepository(_connection);
        var service = new ItemService(itemRepo, new TrashRepository(_connection));
        var item = (await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.RestoreItemAsync(item);

        var restored = await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Equal(folderId, restored!.FolderId);
    }

    [Fact]
    public async Task RestoreItemAsync_RestoresOriginalArchiveStatus()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001", archiveStatus: "archived");
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var itemRepo = new ItemRepository(_connection);
        var service = new ItemService(itemRepo, new TrashRepository(_connection));
        var item = (await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.RestoreItemAsync(item);

        var restored = await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Equal(ArchiveStatus.archived, restored!.ArchiveStatus);
    }

    [Fact]
    public async Task RestoreItemAsync_DeletesTrashRecord()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var itemRepo = new ItemRepository(_connection);
        var trashRepo = new TrashRepository(_connection);
        var service = new ItemService(itemRepo, trashRepo);
        var item = (await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.RestoreItemAsync(item);

        var record = await trashRepo.GetByItemIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Null(record);
    }

    // ==================== PermanentlyDeleteItemAsync ====================

    [Fact]
    public async Task PermanentlyDeleteItemAsync_DeletesItem()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var itemRepo = new ItemRepository(_connection);
        var trashRepo = new TrashRepository(_connection);
        var service = new ItemService(itemRepo, trashRepo);
        var item = (await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.PermanentlyDeleteItemAsync(item);

        var deleted = await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Null(deleted);
    }

    [Fact]
    public async Task PermanentlyDeleteItemAsync_DeletesTrashRecord()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var itemRepo = new ItemRepository(_connection);
        var trashRepo = new TrashRepository(_connection);
        var service = new ItemService(itemRepo, trashRepo);
        var item = (await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001")))!;

        await service.PermanentlyDeleteItemAsync(item);

        var record = await trashRepo.GetByItemIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Null(record);
    }

    // ==================== TrashViewModel Commands ====================

    [Fact]
    public async Task TrashViewModel_RestoreCommand_RefreshesList()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Single(vm.TrashedItems);

        vm.SelectedItem = vm.TrashedItems[0];
        await vm.RestoreSelectedItemCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedItem);
        Assert.Empty(vm.TrashedItems);

        // Item should be back in normal list
        var itemRepo = new ItemRepository(_connection);
        var recent = await itemRepo.GetRecentAsync();
        Assert.Single(recent);
    }

    [Fact]
    public async Task TrashViewModel_PermanentDeleteCommand_RefreshesList()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Single(vm.TrashedItems);

        vm.SelectedItem = vm.TrashedItems[0];
        await vm.PermanentlyDeleteSelectedItemCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedItem);
        Assert.Empty(vm.TrashedItems);

        // Item should be gone
        var itemRepo = new ItemRepository(_connection);
        var byId = await itemRepo.GetByIdAsync(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        Assert.Null(byId);
    }

    [Fact]
    public async Task TrashViewModel_RestoreCommand_NullDoesNotCrash()
    {
        var vm = CreateViewModel();
        vm.SelectedItem = null;
        await vm.RestoreSelectedItemCommand.ExecuteAsync(null);
        Assert.Null(vm.SelectedItem);
    }

    [Fact]
    public async Task TrashViewModel_PermanentDeleteCommand_NullDoesNotCrash()
    {
        var vm = CreateViewModel();
        vm.SelectedItem = null;
        await vm.PermanentlyDeleteSelectedItemCommand.ExecuteAsync(null);
        Assert.Null(vm.SelectedItem);
    }

    [Fact]
    public async Task TrashViewModel_NormalListExcludesDeletedAfterRestore()
    {
        InsertTestItem("00000000-0000-0000-0000-000000000001");
        await TrashItem("00000000-0000-0000-0000-000000000001");

        var vm = CreateViewModel();
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SelectedItem = vm.TrashedItems[0];
        await vm.RestoreSelectedItemCommand.ExecuteAsync(null);

        // Should not appear in trashed list
        Assert.Empty(vm.TrashedItems);

        // Should appear in normal list
        var itemRepo = new ItemRepository(_connection);
        var recent = await itemRepo.GetRecentAsync();
        Assert.Single(recent);
        Assert.Null(recent[0].DeletedAt);
    }
}
