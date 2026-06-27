using Gatherly.Windows.Database;
using Gatherly.Windows.Models;
using Gatherly.Windows.Models.Enums;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gatherly.Windows.Tests;

public class EditItemTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ItemRepository _itemRepo;
    private readonly SearchRepository _searchRepo;

    public EditItemTests()
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
        _searchRepo = new SearchRepository(_connection);
    }

    public void Dispose() => _connection.Dispose();

    private async Task<Item> InsertTestItem(
        string title = "Test Title",
        string? body = "Test Body",
        string? author = "Test Author",
        string? remark = "Test Remark",
        Platform platform = Platform.douyin,
        Guid? customPlatformId = null,
        Guid? folderId = null)
    {
        var item = new Item
        {
            Id = Guid.NewGuid(),
            Title = title,
            Body = body,
            Author = author,
            AuthorId = "author123",
            Remark = remark,
            Platform = platform,
            CustomPlatformId = customPlatformId,
            FolderId = folderId,
            OriginalUrl = "https://example.com/test",
            NormalizedUrl = "https://example.com/test",
            PlatformContentId = "pcid_123",
            ImportDate = DateTimeOffset.UtcNow.AddDays(-1),
            ModifyDate = DateTimeOffset.UtcNow.AddDays(-1),
            PublishDate = DateTimeOffset.UtcNow.AddDays(-5),
            ContentStatus = ContentStatus.normal,
            ArchiveStatus = ArchiveStatus.pending,
            MediaStatus = MediaStatus.complete,
            IsStarred = false,
            Version = 1
        };
        await _itemRepo.InsertAsync(item);
        return item;
    }

    // ==================== 编辑字段 ====================

    [Fact]
    public async Task EditItem_ShouldUpdateTitleBodyAuthorRemark()
    {
        var item = await InsertTestItem();
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);

        fresh.Title = "New Title";
        fresh.Body = "New Body";
        fresh.Author = "New Author";
        fresh.Remark = "New Remark";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(reloaded);
        Assert.Equal("New Title", reloaded.Title);
        Assert.Equal("New Body", reloaded.Body);
        Assert.Equal("New Author", reloaded.Author);
        Assert.Equal("New Remark", reloaded.Remark);
    }

    [Fact]
    public async Task EditItem_EmptyFields_ShouldSaveNull()
    {
        var item = await InsertTestItem();
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);

        fresh.Title = null;
        fresh.Body = null;
        fresh.Author = null;
        fresh.Remark = null;
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.Title);
        Assert.Null(reloaded.Body);
        Assert.Null(reloaded.Author);
        Assert.Null(reloaded.Remark);
    }

    [Fact]
    public async Task EditItem_ShouldUpdateModifyDate()
    {
        var item = await InsertTestItem();
        var oldModifyDate = item.ModifyDate;

        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);

        var newDate = DateTimeOffset.UtcNow;
        fresh.ModifyDate = newDate;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded.ModifyDate > oldModifyDate);
    }

    // ==================== protected fields ====================

    [Fact]
    public async Task EditItem_ShouldNotChangePlatform()
    {
        var item = await InsertTestItem(platform: Platform.douyin);
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "Changed Title";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal(Platform.douyin, reloaded!.Platform);
    }

    [Fact]
    public async Task EditItem_ShouldNotChangeCustomPlatformId()
    {
        var cpId = Guid.NewGuid();
        var item = await InsertTestItem(platform: Platform.custom, customPlatformId: cpId);
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "Changed Title";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal(cpId, reloaded!.CustomPlatformId);
    }

    [Fact]
    public async Task EditItem_ShouldNotChangeFolderId()
    {
        var fId = Guid.NewGuid();
        var item = await InsertTestItem(folderId: fId);
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "Changed Title";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal(fId, reloaded!.FolderId);
    }

    [Fact]
    public async Task EditItem_ShouldNotChangeStatus()
    {
        var item = await InsertTestItem();
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "Changed Title";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal(ContentStatus.normal, reloaded!.ContentStatus);
        Assert.Equal(ArchiveStatus.pending, reloaded.ArchiveStatus);
        Assert.Equal(MediaStatus.complete, reloaded.MediaStatus);
    }

    [Fact]
    public async Task EditItem_ShouldNotChangeOriginalUrl()
    {
        var item = await InsertTestItem();
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "Changed Title";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal("https://example.com/test", reloaded!.OriginalUrl);
    }

    [Fact]
    public async Task EditItem_ShouldNotChangePlatformContentId()
    {
        var item = await InsertTestItem();
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "Changed Title";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal("pcid_123", reloaded!.PlatformContentId);
    }

    [Fact]
    public async Task EditItem_ShouldNotChangeImportDate()
    {
        var item = await InsertTestItem();
        var originalImportDate = item.ImportDate;
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "Changed Title";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal(originalImportDate.ToUnixTimeSeconds(), reloaded!.ImportDate.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task EditItem_ShouldNotChangeAuthorId()
    {
        var item = await InsertTestItem();
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Author = "New Author";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal("author123", reloaded!.AuthorId);
    }

    [Fact]
    public async Task EditItem_ShouldNotChangeIsStarred()
    {
        var item = await InsertTestItem();
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "Changed";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.False(reloaded!.IsStarred);
    }

    // ==================== FTS ====================

    [Fact]
    public async Task EditItem_ShouldUpdateFtsForTitle()
    {
        var item = await InsertTestItem(title: "Original Title XYZ");
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "UniqueNewTitle12345";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var searchResults = await _searchRepo.SearchAsync("UniqueNewTitle12345");
        Assert.Contains(searchResults, r => r.Id == item.Id);
    }

    [Fact]
    public async Task EditItem_ShouldUpdateFtsForBody()
    {
        var item = await InsertTestItem(body: "Original Body");
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Body = "UniqueNewBodyContent67890";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var searchResults = await _searchRepo.SearchAsync("UniqueNewBodyContent67890");
        Assert.Contains(searchResults, r => r.Id == item.Id);
    }

    [Fact]
    public async Task EditItem_FtsOldKeyword_ShouldNotMatchAfterTitleChange()
    {
        var item = await InsertTestItem(title: "OldUniqueKeyword");
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Title = "BrandNewKeyword";
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var oldResults = await _searchRepo.SearchAsync("OldUniqueKeyword");
        Assert.DoesNotContain(oldResults, r => r.Id == item.Id);

        var newResults = await _searchRepo.SearchAsync("BrandNewKeyword");
        Assert.Contains(newResults, r => r.Id == item.Id);
    }

    // ==================== 回归 ====================

    [Fact]
    public async Task MoveToPlatform_ShouldStillWork()
    {
        var cpId = Guid.NewGuid();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "INSERT INTO custom_platforms (id, name, created_at, sort_order) VALUES ($id, $name, $now, 0)";
            cmd.Parameters.AddWithValue("$id", cpId.ToString("D"));
            cmd.Parameters.AddWithValue("$name", "Test Platform");
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.ExecuteNonQuery();
        }

        var item = await InsertTestItem(platform: Platform.douyin);
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.Platform = Platform.custom;
        fresh.CustomPlatformId = cpId;
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal(Platform.custom, reloaded!.Platform);
        Assert.Equal(cpId, reloaded.CustomPlatformId);
    }

    [Fact]
    public async Task MoveToFolder_ShouldStillWork()
    {
        var fId = Guid.NewGuid();
        var item = await InsertTestItem();
        var fresh = await _itemRepo.GetByIdAsync(item.Id);
        Assert.NotNull(fresh);
        fresh.FolderId = fId;
        fresh.ModifyDate = DateTimeOffset.UtcNow;
        await _itemRepo.UpdateAsync(fresh);

        var reloaded = await _itemRepo.GetByIdAsync(item.Id);
        Assert.Equal(fId, reloaded!.FolderId);
    }
}
