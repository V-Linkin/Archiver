using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class FolderCrudTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly FolderService _folderService;
    private readonly FolderRepository _folderRepo;
    private readonly ItemRepository _itemRepo;

    public FolderCrudTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        MigrationRunner.RunAll(_connection);
        _folderRepo = new FolderRepository(_connection);
        _itemRepo = new ItemRepository(_connection);
        _folderService = new FolderService(_folderRepo, _itemRepo);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateFolder_ShouldInsertFolder()
    {
        var folder = await _folderService.CreateFolderAsync("测试文件夹", Platform.custom);

        Assert.NotEqual(Guid.Empty, folder.Id);
        Assert.Equal("测试文件夹", folder.Name);
        Assert.Equal(Platform.custom, folder.Platform);

        var fetched = await _folderRepo.GetByIdAsync(folder.Id);
        Assert.NotNull(fetched);
    }

    [Fact]
    public async Task CreateFolder_EmptyName_ShouldFail()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _folderService.CreateFolderAsync("", Platform.custom));
    }

    [Fact]
    public async Task CreateFolder_WhitespaceName_ShouldFail()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _folderService.CreateFolderAsync("   ", Platform.custom));
    }

    [Fact]
    public async Task RenameFolder_ShouldUpdateName()
    {
        var folder = await _folderService.CreateFolderAsync("原名", Platform.custom);
        await _folderService.RenameFolderAsync(folder.Id, "新名称");

        var fetched = await _folderRepo.GetByIdAsync(folder.Id);
        Assert.NotNull(fetched);
        Assert.Equal("新名称", fetched.Name);
    }

    [Fact]
    public async Task RenameFolder_EmptyName_ShouldFail()
    {
        var folder = await _folderService.CreateFolderAsync("Test", Platform.custom);
        await Assert.ThrowsAsync<ArgumentException>(
            () => _folderService.RenameFolderAsync(folder.Id, ""));
    }

    [Fact]
    public async Task DeleteFolder_ShouldDeleteFolder()
    {
        var folder = await _folderService.CreateFolderAsync("ToDelete", Platform.custom);
        await _folderService.DeleteFolderAsync(folder.Id);

        var fetched = await _folderRepo.GetByIdAsync(folder.Id);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task DeleteFolder_ShouldClearItemFolderId()
    {
        var folder = await _folderService.CreateFolderAsync("WithItems", Platform.custom);
        var item = new Item
        {
            Title = "Test Item",
            OriginalUrl = "https://example.com",
            Platform = Platform.custom,
            NormalizedUrl = "https://example.com",
            FolderId = folder.Id,
            ImportDate = DateTimeOffset.UtcNow,
            ModifyDate = DateTimeOffset.UtcNow
        };
        await _itemRepo.InsertAsync(item);

        await _folderService.DeleteFolderAsync(folder.Id);

        var fetched = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fetched);
        Assert.Null(fetched.FolderId);
    }

    [Fact]
    public async Task DeleteFolder_ShouldNotDeleteItems()
    {
        var folder = await _folderService.CreateFolderAsync("WithItems", Platform.custom);
        var item = new Item
        {
            Title = "Test Item",
            OriginalUrl = "https://example.com",
            Platform = Platform.custom,
            NormalizedUrl = "https://example.com",
            FolderId = folder.Id,
            ImportDate = DateTimeOffset.UtcNow,
            ModifyDate = DateTimeOffset.UtcNow
        };
        await _itemRepo.InsertAsync(item);

        await _folderService.DeleteFolderAsync(folder.Id);

        var fetched = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fetched);
    }

    [Fact]
    public async Task CountItems_ShouldReturnFolderItemCount()
    {
        var folder = await _folderService.CreateFolderAsync("CountTest", Platform.custom);

        Assert.Equal(0, await _folderService.CountItemsAsync(folder.Id));

        var item = new Item
        {
            Title = "InFolder",
            OriginalUrl = "https://example.com",
            Platform = Platform.custom,
            NormalizedUrl = "https://example.com",
            FolderId = folder.Id,
            ImportDate = DateTimeOffset.UtcNow,
            ModifyDate = DateTimeOffset.UtcNow
        };
        await _itemRepo.InsertAsync(item);

        Assert.Equal(1, await _folderService.CountItemsAsync(folder.Id));
    }
}
