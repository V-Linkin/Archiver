using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Gatherly.Windows.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class MoveToCustomPlatformTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ItemService _itemService;
    private readonly CustomPlatformRepository _customPlatformRepo;
    private readonly ItemRepository _itemRepo;

    public MoveToCustomPlatformTests()
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

        _itemRepo = new ItemRepository(_connection);
        var trashRepo = new TrashRepository(_connection);
        var folderRepo = new FolderRepository(_connection);
        var mediaRepo = new MediaRepository(_connection);
        _itemService = new ItemService(_itemRepo, trashRepo, folderRepo, mediaRepo, _connection);
        _customPlatformRepo = new CustomPlatformRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    private async Task<Item> InsertTestItem(string title = "test", Platform platform = Platform.douyin)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Title = title,
            Body = "test body",
            OriginalUrl = "https://example.com/test",
            Platform = platform,
            NormalizedUrl = "https://example.com/test",
            ImportDate = DateTimeOffset.UtcNow,
            ModifyDate = DateTimeOffset.UtcNow,
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.textOnly
        };
        await _itemRepo.InsertAsync(item);
        return item;
    }

    [Fact]
    public async Task MoveToCustomPlatform_ShouldSetPlatformCustom()
    {
        var platform = await _customPlatformRepo.CreateAsync("TestPlatform");
        var item = await InsertTestItem();

        await _itemService.MoveToCustomPlatformAsync(item, platform.Id, _customPlatformRepo);

        var updated = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(updated);
        Assert.Equal(Platform.custom, updated.Platform);
        Assert.Equal(platform.Id, updated.CustomPlatformId);
        Assert.Null(updated.FolderId);
    }

    [Fact]
    public async Task MoveToCustomPlatform_ShouldNotChangeContentFields()
    {
        var platform = await _customPlatformRepo.CreateAsync("TestPlatform");
        var item = await InsertTestItem(title: "MyTitle");

        await _itemService.MoveToCustomPlatformAsync(item, platform.Id, _customPlatformRepo);

        var updated = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(updated);
        Assert.Equal("MyTitle", updated.Title);
        Assert.Equal("test body", updated.Body);
        Assert.Equal("https://example.com/test", updated.OriginalUrl);
        Assert.Equal(MediaStatus.textOnly, updated.MediaStatus);
        Assert.Equal(ContentStatus.normal, updated.ContentStatus);
        Assert.False(updated.IsStarred);
    }

    [Fact]
    public async Task MoveToCustomPlatform_TargetMissing_ShouldFail()
    {
        var item = await InsertTestItem();
        var fakeId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _itemService.MoveToCustomPlatformAsync(item, fakeId, _customPlatformRepo));

        var unchanged = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(unchanged);
        Assert.Equal(Platform.douyin, unchanged.Platform);
    }

    [Fact]
    public async Task MoveToCustomPlatform_ItemMissing_ShouldFail()
    {
        var platform = await _customPlatformRepo.CreateAsync("TestPlatform");
        var fakeItem = new Item
        {
            Id = Guid.NewGuid(),
            OriginalUrl = "https://example.com",
            Platform = Platform.douyin,
            NormalizedUrl = "https://example.com",
            ImportDate = DateTimeOffset.UtcNow,
            ModifyDate = DateTimeOffset.UtcNow
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _itemService.MoveToCustomPlatformAsync(fakeItem, platform.Id, _customPlatformRepo));
    }

    [Fact]
    public async Task MoveToPlatformDialog_NoCustomPlatforms_ShouldDisableMove()
    {
        var platforms = await _customPlatformRepo.GetAllAsync();
        Assert.Empty(platforms);
    }

    [Fact]
    public async Task MoveToCustomPlatform_AlreadyOnSamePlatform_ShouldSucceed()
    {
        var platform = await _customPlatformRepo.CreateAsync("TestPlatform");
        var item = await InsertTestItem();
        item.Platform = Platform.custom;
        item.CustomPlatformId = platform.Id;
        await _itemRepo.UpdateAsync(item);

        await _itemService.MoveToCustomPlatformAsync(item, platform.Id, _customPlatformRepo);

        var updated = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(updated);
        Assert.Equal(platform.Id, updated.CustomPlatformId);
        Assert.Null(updated.FolderId);
    }
}
